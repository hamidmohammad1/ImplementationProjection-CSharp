using System;

namespace ProjectionSemiMarkov
{
  public class Policy
  {
    /// <summary>
    /// The policy id.
    /// </summary>
    public readonly string policyId;

    /// <summary>
    /// The policy age.
    /// </summary>
    public readonly double age;

    /// <summary>
    /// The policy gender.
    /// </summary>
    public readonly Gender gender;

    /// <summary>
    /// The expiry age. Usually age 120, if the policy has a life annuity.
    /// </summary>
    public readonly double expiryAge;

    /// <summary>
    /// The state at initial time.
    /// </summary>
    public readonly State initialState;

    /// <summary>
    /// The initial time
    /// </summary>
    public readonly double initialTime;

    /// <summary>
    /// The duration in initial state at initial time.
    /// </summary>
    public readonly double initialDuration;

    /// <summary>
    /// The original products summed.
    /// </summary>
    public readonly Product originalBenefits;

    /// <summary>
    /// The bonus product.
    /// </summary>
    public readonly Product bonusBenefit;

    /// <summary>
    /// Constructs a policy.
    /// </summary>
    public Policy(
      string policyId,
      double age,
      Gender gender,
      double expiryAge,
      State initialState,
      double initialTime,
      double initialDuration,
      Product originalBenefits,
      Product bonusBenefit)
    {
      if (age > expiryAge)
        throw new ArgumentException("Policy {0}: Age can't be larger than expiryAge", policyId);

      this.policyId = policyId;
      this.age = age;
      this.gender = gender;
      this.expiryAge = expiryAge;
      this.initialState = initialState;
      this.initialTime = initialTime;
      this.initialDuration = initialDuration;
      this.originalBenefits = originalBenefits;
      this.bonusBenefit = bonusBenefit;
    }
  }
}
