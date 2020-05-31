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
}
