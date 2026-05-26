using System.Diagnostics.CodeAnalysis;

namespace Moongazing.OrionShowcase.Application.Pipeline;

[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker interface used by AuditBehavior to opt commands into auditing via generic constraint.")]
public interface IAuditableCommand { }
