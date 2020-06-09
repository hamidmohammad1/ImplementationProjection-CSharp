using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;

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
    private ProbabilityCalculator marketProbabilityCalculator;
    private Dictionary<string, Dictionary<State, double[][]>> probabilities;
    private Dictionary<string, Dictionary<State, double[][]>> rhoProbabilities;
    private Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> technicalReserves;
    Dictionary<string, double[]> freePolicyFactor;

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

      policy1 = new Policy(policyId: "policy1", age: 30, gender: Gender.Male, expiryAge: 90,
        initialState: State.Active, initialDuration: 5, payments: payments);

      policies = new Dictionary<string, Policy>
      {
        { policy1.policyId, policy1 }
      };
      marketBasis = CreateMarketBasisIntensities();

      var technicalReserveCalculator = new TechnicalReserveCalculator();
      technicalReserves = technicalReserveCalculator.Calculate();
      var (originalTechReserves, originalTechPositiveReserves, bonusTechnicalReserves)
        = technicalReserveCalculator.CalculateTechnicalReserve();

      // Calculating \rho = (V_{Active}^{\circ,*,+} + V_{Active}^{\circ,*,-})/ V_{Active}^{\circ,*,+} for each Time point
      freePolicyFactor = technicalReserveCalculator.CalculateFreePolicyFactor();

      marketProbabilityCalculator = new ProbabilityCalculator(time: 0.0, freePolicyFactor,
        originalTechReserves, originalTechPositiveReserves, bonusTechnicalReserves);

      // Initial state must be active or disability, if one wants to calculate RhoModifiedProbabilities
      probabilities = marketProbabilityCalculator.Calculate(calculateRhoProbability: true);
      rhoProbabilities = marketProbabilityCalculator.RhoProbabilities;
    }

    [Test]
    public void ProbabilitiesSumToOneAtUmax()
    {
      for (var t = 0; t < marketProbabilityCalculator.GetNumberOfTimePoints(policy1, marketProbabilityCalculator.Time); t++)
      {
        var umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration,t);
        Assert.That(marketProbabilityCalculator.MarketStateSpace
          .Sum(j => probabilities[policy1.policyId][j][t][umax]), Is.EqualTo(1.0).Within(epsilon));
      }

      for (var i = 0; i <= 90 * 12; i++)
      {
        Console.WriteLine("Years: " + i/12 + " and months " + i % 12 );
        //foreach (var state in marketProbabilityCalculator.MarketStateSpace)
          //Console.WriteLine("Tilstand " + state + " med ssh:  " + probabilities[policy1.policyId][state][i][marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration, i)]);
        foreach (var state in GiveCollectionOfStates(StateCollection.FreePolicyStatesWithSurrender))
          Console.WriteLine("Free police tilstand " + state + " med ssh:  " + rhoProbabilities[policy1.policyId][state][i][marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration, i)]);
        Console.WriteLine("----------------------------------------------------------------");
      }
    }

    [Test]
    public void ProbabilitiesAtActiveAreDurationDependent()
    {
      for (var t = 1; t < marketProbabilityCalculator
        .GetNumberOfTimePoints(policy1, marketProbabilityCalculator.Time); t++)
      {
        Assert.That(probabilities[policy1.policyId][State.Active][t].Distinct().Count(), Is.GreaterThan(1));
      }
    }

    [Test]
    public void IntensitiesAreNotTooLarge()
    {
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

    [Test]
    public void RegressionTestForProbabilities()
    {
      // Expected last five probabilities
      var expectedProbabilities = new List<double>
        {
          1.0,
          0.98893149449659778,
          0.97799283951155114,
          0.96718243333451615,
          0.95649869488622441
        };

      for (var t = 0; t < 5; t++)
      {
        var umax = marketProbabilityCalculator.DurationSupportIndex(policy1.initialDuration, t);
        Assert.That(probabilities[policy1.policyId][State.Active][t][umax],
          Is.EqualTo(expectedProbabilities[t]).Within(epsilon));
      }
    }

    [Test]
    public void RegressionTests()
    {
      /*
      //USE FOR GETTING EXPECTED REGRESSION VALUE
      var s1 = technicalReserves[policy1.policyId][(PaymentStream.Original, Sign.Positive)][State.Active];
      Console.WriteLine(string.Join(", ", s1.Select(x => x.ToString(CultureInfo.InvariantCulture))));
      Console.WriteLine("--------------------------------------------------------------------------------------------------------------------------------");

      var s2 = freePolicyFactor[policy1.policyId];
      Console.WriteLine(string.Join(", ", s2.Select(x => x.ToString(CultureInfo.InvariantCulture))));
      Console.WriteLine("--------------------------------------------------------------------------------------------------------------------------------");

      var s3 = probabilities[policy1.policyId][State.Active][20];
      Console.WriteLine(string.Join(", ", s3.Select(x => x.ToString(CultureInfo.InvariantCulture))));
      Console.WriteLine("--------------------------------------------------------------------------------------------------------------------------------");

      var s4 = rhoProbabilities[policy1.policyId][State.FreePolicyActive][20];
      Console.WriteLine(string.Join(", ", s4.Select(x => x.ToString(CultureInfo.InvariantCulture))));
      */

      var technicalReservesPolicy1 = technicalReserves[policy1.policyId][(PaymentStream.Original, Sign.Positive)][State.Active];
      CollectionAssert.AreEqual(TestSourceForRegressionTests.GetExpectedTechnicalValues(), technicalReservesPolicy1);

      var freePolicyFactorPolicy1 = freePolicyFactor[policy1.policyId];
      CollectionAssert.AreEqual(TestSourceForRegressionTests.GetExpectedFreePolicyValues(), freePolicyFactorPolicy1);

      var probabilitiesPolicy1 = probabilities[policy1.policyId][State.Active][20];
      CollectionAssert.AreEqual(TestSourceForRegressionTests.GetExpectedProbabilities(), probabilitiesPolicy1);

      var rhoModifiedProbabilitiesPolicy1 = rhoProbabilities[policy1.policyId][State.FreePolicyActive][20];
      CollectionAssert.AreEqual(TestSourceForRegressionTests.GetExpectedRhoProbabilities(), rhoModifiedProbabilitiesPolicy1);
    }
  }
}
