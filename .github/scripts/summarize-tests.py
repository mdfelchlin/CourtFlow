#!/usr/bin/env python3
"""Write the dotnet test totals, and any failed tests, to the GitHub step summary."""

import os
import re
import sys
from pathlib import Path

ANSI = re.compile(r"\x1b\[[0-9;]*m")
FAILED_TEST = re.compile(r"^failed\s+(.*)\s+\(([^)]+)\)\s*$")
PROJECT_RESULT = re.compile(
    r"^(?P<indent>\s*)(?P<path>\S+\.dll)\s+\([^)]+\)\s+"
    r"(?P<result>passed|failed)(?:\s+with\s+(?P<errors>\d+)\s+error\(s\))?\s+"
    r"\((?P<duration>[^)]+)\)\s*$"
)
TOTAL_LINE = re.compile(r"^(total|failed|succeeded|skipped|duration):\s*(.+)$")


def project_name(path: str) -> str:
    return Path(path).name.removesuffix(".dll")


def fence(text: str) -> str:
    body = text.replace("```", "'''")
    return f"```text\n{body}\n```"


def parse(text: str) -> tuple[dict[str, str], list[tuple[str, str, str]], list[tuple[str, str, str, str]]]:
    lines = [ANSI.sub("", line).rstrip() for line in text.splitlines()]
    summary_at = next(
        (index for index, line in enumerate(lines) if line.strip().startswith("Test run summary:")),
        None,
    )
    totals: dict[str, str] = {}
    projects: list[tuple[str, str, str]] = []
    if summary_at is not None:
        for line in lines[summary_at + 1 : summary_at + 40]:
            stripped = line.strip()
            match = TOTAL_LINE.match(stripped)
            if match:
                totals[match.group(1)] = match.group(2).strip()
                continue
            project = PROJECT_RESULT.match(line)
            if project:
                projects.append(
                    (
                        project_name(project.group("path")),
                        project.group("result"),
                        project.group("duration"),
                    )
                )
            elif totals and stripped and not stripped.startswith("/"):
                break
    if not projects:
        seen: set[str] = set()
        for line in lines:
            project = PROJECT_RESULT.match(line)
            if not project:
                continue
            name = project_name(project.group("path"))
            if name in seen:
                continue
            seen.add(name)
            projects.append((name, project.group("result"), project.group("duration")))

    failures: list[tuple[str, str, str, str]] = []
    limit = summary_at if summary_at is not None else len(lines)
    index = 0
    while index < limit:
        match = FAILED_TEST.match(lines[index])
        if not match:
            index += 1
            continue
        name, duration = match.group(1), match.group(2)
        index += 1
        body: list[str] = []
        assembly = ""
        while index < limit:
            line = lines[index]
            if FAILED_TEST.match(line) or line.strip().startswith("Test run summary:"):
                break
            if PROJECT_RESULT.match(line) and not line.startswith((" ", "\t")):
                break
            stripped = line.strip()
            from_assembly = re.match(r"from\s+(\S+\.dll)\b", stripped)
            if from_assembly and not assembly:
                assembly = project_name(from_assembly.group(1))
            else:
                body.append(line)
            index += 1
        message = "\n".join(body).strip()
        if len(message.splitlines()) > 40:
            message = "\n".join(message.splitlines()[:40]) + "\n…"
        failures.append((name, duration, assembly, message))
    return totals, projects, failures


def markdown(totals: dict[str, str], projects: list[tuple[str, str, str]], failures: list[tuple[str, str, str, str]], outcome: str) -> str:
    lines = ["## Test results", ""]
    if not totals:
        lines.append("The run stopped before it reported test totals.")
        if outcome:
            lines.append("")
            lines.append(f"The test step outcome was `{outcome}`.")
        return "\n".join(lines) + "\n"

    passed = totals.get("succeeded", "0")
    failed = totals.get("failed", "0")
    lines.extend(
        [
            "| | Count |",
            "| --- | ---: |",
            f"| Total | {totals.get('total', '0')} |",
            f"| Passed | {passed} |",
            f"| Failed | {failed} |",
            f"| Skipped | {totals.get('skipped', '0')} |",
            "",
        ]
    )
    if "duration" in totals:
        lines.append(f"Duration: {totals['duration']}")
        lines.append("")
    if projects:
        lines.extend(["### Projects", "", "| Project | Result | Duration |", "| --- | --- | --- |"])
        for name, result, duration in projects:
            lines.append(f"| {name.replace('|', '\\|')} | {result} | {duration} |")
        lines.append("")
    if failures:
        lines.extend(["### Failures", ""])
        for name, duration, assembly, message in failures:
            where = f" in `{assembly}`" if assembly else ""
            lines.append(f"#### `{name}` ({duration}){where}")
            lines.append("")
            if message:
                lines.append(fence(message))
                lines.append("")
    elif failed != "0":
        lines.append("Tests failed, and the log did not list their names.")
        lines.append("")
    return "\n".join(lines)


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: summarize-tests.py <test-output>", file=sys.stderr)
        return 2
    path = Path(sys.argv[1])
    if not path.is_file():
        text = ""
    else:
        text = path.read_text(encoding="utf-8", errors="replace")
    totals, projects, failures = parse(text)
    summary = markdown(totals, projects, failures, os.environ.get("TEST_OUTCOME", ""))
    print(summary, end="" if summary.endswith("\n") else "\n")
    destination = os.environ.get("GITHUB_STEP_SUMMARY")
    if destination:
        with open(destination, "a", encoding="utf-8") as handle:
            handle.write(summary if summary.endswith("\n") else summary + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
