namespace BusinessIdentityServer.Internals.Constants.DataProtection;

// defines strongly-typed protection purposes for isolating time-limited encrypted tokens
public enum ProtectionPurpose
{
    // encrypts temporary unconfirmed TOTP registration keys and state during authenticator app setup
    TotpRegistrationSession,

    // encrypts email verification challenge tokens during user registration/sign up
    EmailSignupChallenge,

    // encrypts access recovery challenge tokens when resetting or recovering TOTP access
    TotpAccessRecovery
}

