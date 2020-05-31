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
    private double epsilon = Math.Pow(10, -15);
    private Policy policy1;
    private Dictionary<string, Policy> policies;
    private Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> marketBasis;
    private (Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>>, double) technicalBasis;
    private IEnumerable<State> stateSpace;

    [SetUp]
    public void Setup() //run before each test
    {
      var lifeAnnuityProduct = CreateLifeAnnuity(1000);
      var premiumProduct = CreatePremiumPayment(-200);
      var deferredDisabilityAnnuity = CreateDeferredDisabilityAnnuity(500);

      var policy1Premium = new List<Product> { premiumProduct };
      var policy1Benefits = new List<Product> { lifeAnnuityProduct, deferredDisabilityAnnuity };

      var payments = new Dictionary<(PaymentStream, Sign), Product>
        {
          { (PaymentStream.Original, Sign.Positive), SumProducts(policy1Benefits) },
          { (PaymentStream.Original, Sign.Negative), SumProducts(policy1Premium) },
          { (PaymentStream.Bonus, Sign.Positive), lifeAnnuityProduct }
        };

      policy1 = new Policy(policyId: "policy1", age: 30, gender: Gender.Male, expiryAge: 120 - 30,
        initialState: State.Active, initialTime: 0, initialDuration: 5, payments: payments);

      policies = new Dictionary<string, Policy>
      {
        { policy1.policyId, policy1 }
      };
      epsilon = Math.Pow(10, -15);
      marketBasis = CreateMarketBasisIntensities();
      technicalBasis = CreateTechnicalBasisIntensities();

      var allPossibleTransitions = marketBasis[Gender.Female].Union(marketBasis[Gender.Male]).ToList();
      stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      //stateSpace = new [] { State.Active,State.Disabled,State.Dead,State.Surrender,State.FreePolicyActive,
      //  State.FreePolicyDisabled,State.FreePolicyDead,State.FreePolicySurrender, };
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
    public void FirstFiveProbabilitiesAtUmaxAtActiveAreUnchanged()
    {
      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);
      var probabilities = marketProbabilityCalculator.Calculate();

      var targetProbabilities = new List<double>
      {
        1.0,
        0.98893149449659778,
        0.97799283951155114,
        0.96718243333451615,
        0.95649869488622441
      };

      for (var t = 0; t < 5; t++)
      {
        int umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration,t);
        Assert.That(probabilities[policy1.policyId][State.Active][t][umax], Is.EqualTo(targetProbabilities[t]).Within(epsilon));
      }
    }

    [Test]
    public void ProbabilitiesAtActiveAreDurationDependent()
    {
      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);
      var probabilities = marketProbabilityCalculator.Calculate();

      for (var t = 1; t < marketProbabilityCalculator.GetNumberOfTimePoints(policy1); t++)
      {
        var probabilityList = probabilities[policy1.policyId][State.Active][t].Distinct();
        var m = 0;
        foreach(var e in probabilities[policy1.policyId][State.Active][t].Distinct())
        {
          m=m+1;
        }
        Assert.That(m,Is.Not.EqualTo(1));
      }
    }

    [Test]
    public void IntensitiesAreNotTooLarge()
    {
      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);

      foreach (var g in Enum.GetValues(typeof(Gender)).Cast<Gender>())
      {
         var genderIntensity = marketBasis[g];
        foreach (var j in genderIntensity.Keys)
        {
          foreach (var i in genderIntensity[j].Keys)
          {
            for (var t = 0; t <= policy1.expiryAge; t++)
            {
              int umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration, t);
              for (var u = 0; u <= umax; u++)
              {
                Assert.That(marketBasis[g][j][i](t, u), Is.LessThanOrEqualTo(10));
              }
            }
          }
        }
      }
    }

    [Test]
    public void MuDotForActiveIsSumOfIntensities()
    {
      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);

      for (var t = 0; t <= policy1.expiryAge; t++)
      {
        int umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration, t);
        for (var u = 0; u <= umax; u++)
        {
          Assert.That(marketBasis[policy1.gender][State.Active][State.Active](t, u), Is.EqualTo(
            marketBasis[policy1.gender][State.Active][State.Disabled](t, u) + 
            marketBasis[policy1.gender][State.Active][State.FreePolicyActive](t, u) +
            marketBasis[policy1.gender][State.Active][State.Dead](t, u) +
            marketBasis[policy1.gender][State.Active][State.Surrender](t, u)
            ).Within(epsilon));
        }
      }
    }

  }
}
