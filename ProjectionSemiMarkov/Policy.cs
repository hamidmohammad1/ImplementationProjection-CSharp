using System;
using System.Collections.Generic;

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
    /// The payments.
    /// </summary>
    public Dictionary<(PaymentStream, Sign), Product> Payments { get; private set;  }

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
      Dictionary<(PaymentStream, Sign), Product> payments)
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
      this.Payments = payments;
    }
  }

  public class Product
  {
    public Dictionary<State, Func<double, double>> TechnicalContinuousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double>>> TechnicalJumpPayment { get; }

    public Dictionary<State, Func<double, double, double>> MarketContinuousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double, double>>> MarketJumpPayment { get; }

    public ProductCollection ProductType { get; }

    public Product(
      Dictionary<State, Func<double, double>> technicalContinuousPayment,
      Dictionary<State, Dictionary<State, Func<double, double>>> technicalJumpPayment,
      Dictionary<State, Func<double, double, double>> marketContinuousPayment,
      Dictionary<State, Dictionary<State, Func<double, double, double>>> marketJumpPayment,
      ProductCollection productType)
    {
      this.TechnicalContinuousPayment = technicalContinuousPayment ?? new Dictionary<State, Func<double, double>>();
      this.TechnicalJumpPayment = technicalJumpPayment ?? new Dictionary<State, Dictionary<State, Func<double, double>>>();
      this.MarketContinuousPayment = marketContinuousPayment ?? new Dictionary<State, Func<double, double, double>>();
      this.MarketJumpPayment = marketJumpPayment ?? new Dictionary<State, Dictionary<State, Func<double, double, double>>>();
      this.ProductType = productType;
    }
  }
}
