using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Abstractions;
using CourtFlow.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class TemplatesController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITenantContext _tenant;

    public TemplatesController(IAppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await _db.TournamentTemplates
            .OrderByDescending(t => t.UpdatedAtUtc)
            .Select(t => new TemplateDto(t.Id, t.Name, t.Description, t.SourceTournamentId, t.UsageCount, t.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(templates);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var t = await _db.TournamentTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        return Ok(new TemplateDto(t.Id, t.Name, t.Description, t.SourceTournamentId, t.UsageCount, t.CreatedAtUtc));
    }

    [HttpPost("capture")]
    public async Task<IActionResult> Capture([FromBody] CaptureTemplateBody body, CancellationToken ct)
    {
        var orgId = _tenant.OrganizationId ?? throw new UnauthorizedAccessException();
        var tournament = await _db.Tournaments.FirstOrDefaultAsync(t => t.Id == body.SourceTournamentId, ct);
        if (tournament is null) return NotFound();

        var template = new TournamentTemplate(orgId, body.Name ?? $"Template from {tournament.Name}", body.SourceTournamentId);
        _db.TournamentTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = template.Id },
            new TemplateDto(template.Id, template.Name, template.Description, template.SourceTournamentId, template.UsageCount, template.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var t = await _db.TournamentTemplates.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        _db.TournamentTemplates.Remove(t);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public sealed record TemplateDto(Guid Id, string Name, string? Description, Guid? SourceTournamentId, int UsageCount, DateTime CreatedAt);
public sealed record CaptureTemplateBody(Guid SourceTournamentId, string? Name);
