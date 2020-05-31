using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

using ProjectionSemiMarkov;
using static ProjectionSemiMarkov.HelperFunctions;
using static ProjectionSemiMarkov.Setup;

namespace NUnitTests
{
  [TestFixture]
  public class Tests
  {
    private Policy policy1;
    private Dictionary<string, Policy> policies;
    private double epsilon;
    private Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> marketBasis;
    private (Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>>, double) technicalBasis;
    private IEnumerable<State> stateSpace;

    [SetUp]
    public void Setup() //run before each test
    {
      var lifeAnnuityProduct  = CreateLifeAnnuity(1000.0);
      var policy1Products = new List<Product> { lifeAnnuityProduct };
      policy1 = new Policy(policyId: "policy1", age: 30, gender: Gender.Male, expiryAge: 120 - 30, initialState: State.Active,
          initialTime: 0, initialDuration: 5, originalBenefits: SumProducts(policy1Products), bonusBenefit: lifeAnnuityProduct);

      policies =  new Dictionary<string, Policy>();
      policies.Add(policy1.policyId, policy1);
      epsilon = Math.Pow(10, -15);
      marketBasis = CreateMarketBasisIntensities();
      technicalBasis = CreateTechnicalBasisIntensities();
      stateSpace = new [] { State.Active,State.Disabled,State.Dead,State.Surrender,State.FreePolicyActive,
        State.FreePolicyDisabled,State.FreePolicyDead,State.FreePolicySurrender, };
    }

    [Test]
    public void ProbabilitiesSumToOneAtUmax()
    {
      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);
      var probabilities = marketProbabilityCalculator.Calculate();

      for (var t = 0; t < marketProbabilityCalculator.GetNumberOfTimePoints(policy1); t++)
      {
        int umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration,t);
        Assert.That(stateSpace.Sum(j => probabilities[policy1.policyId][j][t][umax]), Is.EqualTo(1.0).Within(epsilon));
      }
    }

    [Test]
    public void Test2()
    {
      var x = -1;
      Assert.Negative(x);
    }
  }
}
