using FluentValidation;
using MediatR;

namespace CourtFlow.Application.Behaviors;

public sealed class RequestValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
            return await next().ConfigureAwait(false);

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, ct).ConfigureAwait(false);
            if (!result.IsValid)
                failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next().ConfigureAwait(false);
    }
}
