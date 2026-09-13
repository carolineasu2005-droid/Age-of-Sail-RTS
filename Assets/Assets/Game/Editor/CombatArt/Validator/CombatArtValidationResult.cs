using System;

public enum CombatArtValidationSeverity
{
    PASS,
    WARNING,
    ERROR
}

public sealed class CombatArtValidationResult
{
    public CombatArtValidationResult(
        string contractId,
        CombatArtValidationSeverity severity,
        string message,
        UnityEngine.Object targetObject
    )
    {
        if (string.IsNullOrWhiteSpace(contractId)
            || !contractId.StartsWith("ART-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A stable ART contract ID is required.",
                nameof(contractId)
            );
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "A validation message is required.",
                nameof(message)
            );
        }

        if (targetObject == null)
        {
            throw new ArgumentNullException(nameof(targetObject));
        }

        ContractId = contractId;
        Severity = severity;
        Message = message;
        TargetObject = targetObject;
    }


    public string ContractId { get; }

    public CombatArtValidationSeverity Severity { get; }

    public string Message { get; }

    public UnityEngine.Object TargetObject { get; }
}
