using System;
using System.Collections.Generic;
using System.Linq;

using static ProjectionSemiMarkov.HelperFunctions;


namespace ProjectionSemiMarkov
{
  public static class Setup
  {
    static double pensionAge = 67.0;

    /// <summary>
    /// Creates a nested dictionary indexed first on fromState, secondly toState and thirdly gender
    /// giving intensities as function of age and duration on market basis.
    /// </summary>
    /// market interest from FT 5/25/2020 cf.
    /// https://www.finanstilsynet.dk/tal-og-fakta/oplysninger-for-virksomheder/oplysningstal-om-forsikring-og-pension/diskonteringssatser
    /// <returns>Nested dictionaries containing function of doubles to doubles.</returns>
    public static Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>>
      CreateMarketBasisIntensities()
    {
      // Intensity Active --> Disabled
      Func<double, double, double> intensityActiveDisabledMan =
        (age, duration) => Math.Max(0.000075 + Math.Pow(10, 0.0386 * age + 5.371456 - 10), Math.Pow(10, -4));
      Func<double, double, double> intensityActiveDisabledFemale =
        (age, duration) => Math.Max(-0.000908 + Math.Pow(10, 0.026539 * age + 6.591359 - 10), Math.Pow(10, -4));

      // Intensity Active --> Dead
      Func<double, double, double> intensityActiveDeadMan =
        (age, duration) => 0.000069 + Math.Pow(10, 0.049553 * age + 4.776691 - 10);
      Func<double, double, double> intensityActiveDeadFemale =
        (age, duration) => 0.000049 + Math.Pow(10, 0.049055 * age + 4.667086 - 10);

      // Intensity Active --> Surrender
      Func<double, double, double> intensityActiveSurrenderUnisex =
        (age, duration) => (0.0522 - 0.0011 * Math.Max(age - 30, 0)) * LessThanIndicator(age, 60);

      // Intensity Active --> FreePolicyActive
      Func<double, double, double> intensityActiveFreePolicyActiveUnisex =
        (age, duration) => 0.08 * LessThanIndicator(age, pensionAge);

      // Intensity Disabled --> Active
      Func<double, double, double> intensityDisabledActiveMen =
        (age, duration) => duration <= 2
          ? Math.Max(0, 0.485408 - 0.006058 * Math.Max(age, 24))
          : Math.Max(0, 0.103816 - 0.001861 * Math.Max(age, 29));

      Func<double, double, double> intensityDisabledActiveFemale =
        (age, duration) => duration <= 2
          ? Math.Max(0, 0.751028 - 0.010992 * Math.Max(age, 24))
          : Math.Max(0, 0.155466 - 0.003030 * Math.Max(age, 29));

      // Intensity Disabled --> Dead
      Func<double, double, double> intensityDisabledDeadMen =
        (age, duration) => duration <= 2
          ? 0.019292 + Math.Pow(10, 0.047961 * age + 6.030109 - 10)
          : 0.010339 + Math.Pow(10, 0.05049 * age + 5.070927 - 10);

      Func<double, double, double> intensityDisabledDeadFemale =
        (age, duration) => duration <= 2
          ? -0.182547 + Math.Pow(10, 0.00345 * age + 9.166944 - 10)
          : 0.005539 + Math.Pow(10, 0.076478 * age + 3.266007 - 10);

      // Setting up the dictionary
      var tuplesOfIntensity = new List<Tuple<Gender, State, State, Func<double, double, double>>>
      {
        Tuple.Create(Gender.Male, State.Active, State.Disabled, intensityActiveDisabledMan ),
        Tuple.Create(Gender.Male, State.Active, State.Dead, intensityActiveDeadMan ),
        Tuple.Create(Gender.Male, State.Active, State.Surrender, intensityActiveSurrenderUnisex ),
        Tuple.Create(Gender.Male, State.Active, State.FreePolicyActive, intensityActiveFreePolicyActiveUnisex ),
        Tuple.Create(Gender.Male, State.Disabled, State.Active, intensityDisabledActiveMen ),
        Tuple.Create(Gender.Male, State.Disabled, State.Dead, intensityDisabledDeadMen ),
        Tuple.Create(Gender.Male, State.FreePolicyActive, State.FreePolicyDisabled, intensityActiveDisabledMan ),
        Tuple.Create(Gender.Male, State.FreePolicyActive, State.FreePolicyDead, intensityActiveDeadMan ),
        Tuple.Create(Gender.Male, State.FreePolicyActive, State.FreePolicySurrender, intensityActiveSurrenderUnisex ),
        Tuple.Create(Gender.Male, State.FreePolicyDisabled, State.FreePolicyActive, intensityDisabledActiveMen ),
        Tuple.Create(Gender.Male, State.FreePolicyDisabled, State.FreePolicyDead, intensityDisabledDeadMen ),
        Tuple.Create(Gender.Female, State.Active, State.Disabled, intensityActiveDisabledFemale ),
        Tuple.Create(Gender.Female, State.Active, State.Dead, intensityActiveDeadFemale ),
        Tuple.Create(Gender.Female, State.Active, State.Surrender, intensityActiveSurrenderUnisex ),
        Tuple.Create(Gender.Female, State.Active, State.FreePolicyActive, intensityActiveFreePolicyActiveUnisex ),
        Tuple.Create(Gender.Female, State.Disabled, State.Active, intensityDisabledActiveFemale ),
        Tuple.Create(Gender.Female, State.Disabled, State.Dead, intensityDisabledDeadFemale ),
        Tuple.Create(Gender.Female, State.FreePolicyActive, State.FreePolicyDisabled, intensityActiveDisabledFemale ),
        Tuple.Create(Gender.Female, State.FreePolicyActive, State.FreePolicyDead, intensityActiveDeadFemale ),
        Tuple.Create(Gender.Female, State.FreePolicyActive, State.FreePolicySurrender, intensityActiveSurrenderUnisex ),
        Tuple.Create(Gender.Female, State.FreePolicyDisabled, State.FreePolicyActive, intensityDisabledActiveFemale ),
        Tuple.Create(Gender.Female, State.FreePolicyDisabled, State.FreePolicyDead, intensityDisabledDeadFemale ),
      };

      var intensities = tuplesOfIntensity.GroupBy(x => x.Item1, x => Tuple.Create(x.Item2, x.Item3, x.Item4))
        .ToDictionary(
          y => y.Key,
          y => y.GroupBy(z => z.Item1, z => Tuple.Create(z.Item2, z.Item3))
            .ToDictionary(v => v.Key, v => v.ToDictionary(k => k.Item1, k => k.Item2)));

      // Adding intensities j --> j as all intensities out summed together
      foreach (var (gender, v1) in intensities)
      {
        foreach (var (fromState, v2) in v1)
        {
          var theIntensitiesToSum = tuplesOfIntensity.Where(x => x.Item1 == gender && x.Item2 == fromState).Select(x => x.Item4);
          v2.Add(fromState, (age, duration) => theIntensitiesToSum.Sum(x => x(age, duration)));
        }
      }

      return intensities;
    }

    /// <summary>
    /// Creates a nested dictionary indexed first on gender, secondly fromState and thirdly toState
    /// giving intensities as function of age and duration on technical basis.
    /// </summary>
    /// The following intensities are based on page 83-84
    /// https://www.finanstilsynet.dk/~/media/Tal-og-fakta/2014/TG_LIV/PFA-Pension_884946.pdf?la=da&fbclid=IwAR37lGQpceV9e1Zrbrcbb02_B0TM0ZmhMdlVagmoqTGs1IPWRnuH_TgiDTo
    /// <returns>Nested dictionaries containing function of doubles to doubles.</returns>
    public static (Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>>, double)
      CreateTechnicalBasisIntensities()
    {
      // Technical interest
      var technicalInterest = 0.02;

      // Intensity Active --> Disabled
      Func<double, double, double> intensityActiveDisabledUnisex =
        (age, duration) => 0.000400 + Math.Pow(10, 5.26 + 0.048 * age - 10);

      // Intensity Active --> Dead
      Func<double, double, double> intensityActiveDeadUnisex =
        (age, duration) => 0.000600 + Math.Pow(10, 5.6 + 0.040 * age - 10);

      // Intensity Disabled --> Dead
      Func<double, double, double> intensityDisabledDeadUnisex =
        (age, duration) => 0.000600 + Math.Pow(10, 5.6 + 0.040 * age - 10);

      // Setting up the dictionary
      var tuplesOfIntensity = new List<Tuple<Gender, State, State, Func<double, double, double>>>
      {
        Tuple.Create(Gender.Male, State.Active, State.Disabled, intensityActiveDisabledUnisex ),
        Tuple.Create(Gender.Male, State.Active, State.Dead, intensityActiveDeadUnisex ),
        Tuple.Create(Gender.Male, State.Disabled, State.Dead, intensityDisabledDeadUnisex ),
        Tuple.Create(Gender.Female, State.Active, State.Disabled, intensityActiveDisabledUnisex ),
        Tuple.Create(Gender.Female, State.Active, State.Dead, intensityActiveDeadUnisex ),
        Tuple.Create(Gender.Female, State.Disabled, State.Dead, intensityDisabledDeadUnisex ),
      };

      var intensities = tuplesOfIntensity.GroupBy(x => x.Item1, x => Tuple.Create(x.Item2, x.Item3, x.Item4))
        .ToDictionary(
          y => y.Key,
          y => y.GroupBy(z => z.Item1, z => Tuple.Create(z.Item2, z.Item3))
            .ToDictionary(v => v.Key, v => v.ToDictionary(k => k.Item1, k => k.Item2)));

      // Adding intensities j --> j as all intensities out summed together
      foreach (var (gender, v1) in intensities)
      {
        foreach (var (fromState, v2) in v1)
        {
          var theIntensitiesToSum = tuplesOfIntensity.Where(x => x.Item1 == gender && x.Item2 == fromState).Select(x => x.Item4);
          v2.Add(fromState, (age, duration) => theIntensitiesToSum.Sum(x => x(age, duration)));
        }
      }

      return (intensities, technicalInterest);
    }

    /// <summary>
    /// Creates a dictionary with policies
    /// </summary>
    /// <returns>Dictionary with policies.</returns>
    public static Dictionary<string, Policy> CreatePolicies()
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

      var policy1 = new Policy(policyId: "policy1", age: 30, gender: Gender.Male, expiryAge: 120 - 30,
        initialState: State.Active, initialDuration: 5, payments: payments);

      var dicOfPolicies = new Dictionary<string, Policy>
      {
        { policy1.policyId, policy1 }
      };

      return dicOfPolicies;
    }

    /// <summary>
    /// Creates a life annuity product, pays after age 67 until death
    /// </summary>
    /// <returns>A life annuity product.</returns>
    public static Product CreateLifeAnnuity(double value)
    {
      Func<double, double> paymentsTechnical = x => GreaterThanIndicator(x, pensionAge) * value;
      Func<double, double, double> paymentsMarket = (x, y) => paymentsTechnical(x);

      var technicalContinuousPayments = new Dictionary<State, Func<double, double>>
      {
        { State.Active, paymentsTechnical },
        { State.Disabled, paymentsTechnical }
      };

      var marketContinuousPayments = new Dictionary<State, Func<double, double, double>>
      {
        { State.Active, paymentsMarket },
        { State.Disabled, paymentsMarket }
      };

      return new Product(technicalContinuousPayments, null, marketContinuousPayments, null,
        ProductCollection.LifeAnnuity);
    }

    /// <summary>
    /// Creates a premium product, paying continuous premium until age 67.
    /// </summary>
    /// <returns>A premium product.</returns>
    public static Product CreatePremiumPayment(double value)
    {
      Func<double, double> paymentsTechnical = x => LessThanIndicator(x, pensionAge) * value;
      Func<double, double, double> paymentsMarket = (x, y) => paymentsTechnical(x);

      var technicalContinuousPayments = new Dictionary<State, Func<double, double>>
      {
        { State.Active, paymentsTechnical },
        { State.Disabled, paymentsTechnical }
      };

      var marketContinuousPayments = new Dictionary<State, Func<double, double, double>>
      {
        { State.Active, paymentsMarket },
        { State.Disabled, paymentsMarket }
      };

      return new Product(technicalContinuousPayments, null, marketContinuousPayments, null,
        ProductCollection.Premium);
    }

    /// <summary>
    /// Created a 1-year deferred disability annuity running to pension age or reactivation.
    /// Possible to get annuity upon many disability.
    /// </summary>
    /// <remarks>
    /// On technical reserve we do not model the duration, so the insured get the payment from entering the
    /// disability state (often til expiry age due to no reactivation).
    /// </remarks>
    /// <returns>A deferred disability annuity.</returns>
    public static Product CreateDeferredDisabilityAnnuity(double value)
    {
      Func<double, double> paymentsTechnical =
        x => LessThanIndicator(x, pensionAge) * value;

      Func<double, double, double> paymentsMarket =
        (x, u) => LessThanIndicator(x, pensionAge) * GreaterThanIndicator(u, 1) * value;

      var technicalContinuousPayments = new Dictionary<State, Func<double, double>>
        {
          { State.Disabled, paymentsTechnical },
        };

      var marketContinuousPayments = new Dictionary<State, Func<double, double, double>>
        {
          { State.Disabled, paymentsMarket }
        };

      return new Product(technicalContinuousPayments, null, marketContinuousPayments, null,
        ProductCollection.DeferredDisabilityAnnuity);
    }
  }
}