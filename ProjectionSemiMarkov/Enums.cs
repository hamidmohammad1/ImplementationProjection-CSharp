using System;

namespace ProjectionSemiMarkov
{
  public enum State
  {
    Active,
    Disabled,
    Dead,
    Surrender,
    FreePolicyActive,
    FreePolicyDisabled,
    FreePolicyDead,
    FreePolicySurrender,
  }

  public enum Gender
  {
    Male,
    Female,
  }

  public enum ProductCollection
  {
    LifeAnnuity,
    Premium,
    DeferredDisabilityAnnuity,
    SumOfProducts,
  }

  public enum Sign
  {
    Negative,
    Positive,
  }

  public enum PaymentStream
  {
    Original,
    Bonus,
  }

  public enum DividendType
  {
    Continuous0,
    Continuous1,
    Jump0,
    Jump1
  }
}
