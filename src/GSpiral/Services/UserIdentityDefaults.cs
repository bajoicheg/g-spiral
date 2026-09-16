namespace GSpiral.Services;

public sealed record UserIdentityDefaults(string RespondentName, string CompanyName)
{
    public static UserIdentityDefaults Detect() =>
        new(Environment.UserName, Environment.UserDomainName);
}
