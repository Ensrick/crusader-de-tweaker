// Systems/Verification/DamageVerifierBase.cs
namespace CrusaderDETweaker.Systems.Verification
{
    /// <summary>
    /// Base class for all damage verifiers.
    /// Provides common functionality for verification systems.
    /// </summary>
    internal abstract class DamageVerifierBase : IDamageVerifier
    {
        public abstract string VerifierName { get; }
        public abstract VerificationResult Verify();

        protected VerificationResult CreateResult(string type)
            => new VerificationResult { VerificationType = type };
    }
}
