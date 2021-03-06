using FluentValidation;

namespace Piipan.Match.Orchestrator
{
    public class MatchQueryRequestValidator : AbstractValidator<MatchQueryRequest>
    {
        public MatchQueryRequestValidator()
        {
            RuleFor(m => m.Query.First).NotEmpty();
            RuleFor(m => m.Query.Last).NotEmpty();
            RuleFor(m => m.Query.Ssn).Matches(@"^[0-9]{3}-[0-9]{2}-[0-9]{4}$");
        }
    }
}
