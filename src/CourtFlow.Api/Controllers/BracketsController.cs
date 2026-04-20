using CourtFlow.Api.Infrastructure;
using CourtFlow.Application.Features.Brackets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Controllers;

[ApiController]
[Route("api/v1/brackets")]
[Authorize(Policy = AuthorizationPolicies.RequireOrg)]
public class BracketsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BracketsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? divisionId, CancellationToken ct) =>
        Ok(await _mediator.Send(new ListBracketsQuery(divisionId), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetBracketQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateBracketCommand cmd, CancellationToken ct) =>
        Ok(await _mediator.Send(cmd, ct));
}
