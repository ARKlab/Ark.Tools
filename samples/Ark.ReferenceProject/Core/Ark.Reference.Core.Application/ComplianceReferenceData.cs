using Ark.Tools.Compliance;

namespace Ark.Reference.Core.Application;

internal static class ComplianceReferenceData
{
    internal static string _mask(PersonName? value)
    {
        if (value is not PersonName person)
            return string.Empty;

        var cleartext = person.Reveal(CompliancePurpose.Custom("Reference application diagnostics"));
        var redactor = ArkMaskingRedactor.Instance;
        var buffer = new char[redactor.GetRedactedLength(cleartext.AsSpan())];
        var length = redactor.Redact(cleartext.AsSpan(), buffer);
        return new string(buffer, 0, length);
    }
}
