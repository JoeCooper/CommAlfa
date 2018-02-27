namespace Server.Models
{
    public enum RegistrationFailureReasons
    {
        EmailIsInvalid,
        EmailIsInUse,
        PasswordIsInadequate,
        DisplayNameIsBlank,
        CaptchaFail
    }
}
