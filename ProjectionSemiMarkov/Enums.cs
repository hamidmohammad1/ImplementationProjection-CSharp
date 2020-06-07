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

  public enum StateCollection
  {
    Standard,
    StandardWithSurrender,
    FreePolicyStates,
    FreePolicyStatesWithSurrender,
    RhoModifiedFromStates,
    AllStates,
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

  public enum ContinousDividend
  {
    Continuous0,
    Continuous1,
  }

  public enum JumpDividend
  {
    Jump0,
    Jump1
  }

  public enum Assets
  {
    ShortRate,
    RiskyAsset,
  }
}
