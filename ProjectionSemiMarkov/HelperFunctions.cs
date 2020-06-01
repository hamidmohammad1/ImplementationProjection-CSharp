using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectionSemiMarkov
{
  public static class HelperFunctions
  {
    /// <summary>
    /// A less than indicator function.
    /// </summary>
    /// <returns>
    /// If x <= y, then it return one, otherwise zero.
    /// </returns>
    public static double LessThanIndicator(double x, double y)
    {
      return x <= y ? 1 : 0;
    }

    /// <summary>
    /// A greater than indicator function.
    /// </summary>
    /// <returns>
    /// If x => y, then it return one, otherwise zero.
    /// </returns>
    public static double GreaterThanIndicator(double x, double y)
    {
      return x >= y ? 1 : 0;
    }

    /// <summary>
    /// Indicates if double is a integer.
    /// </summary>
    /// <returns>
    /// If x is an integer, then it returns true, otherwise false
    /// </returns>
    public static bool doubleIsInteger(double x)
    {
      return Math.Abs(x % 1) <= (double.Epsilon * 100);
    }

    public static Product SumProducts(List<Product> products)
    {
      var technicalContinousPayment = products.SelectMany(x => x.TechnicalContinuousPayment).GroupBy(x => x.Key, x => x.Value)
        .ToDictionary(x => x.Key, x => (Func<double, double>)(k => x.Sum(z => z(k))));

      var technicalJumpPayment = products.SelectMany(x => x.TechnicalJumpPayment).GroupBy(x => x.Key, x => x.Value.ToList())
        .ToDictionary(x => x.Key, x => x.SelectMany(z => z).GroupBy(z => z.Key, z => z.Value)
          .ToDictionary(y => y.Key, y => (Func<double, double>)(k => y.Sum(z => z(k)))));

      var marketContinousPayment = products.SelectMany(x => x.MarketContinuousPayment).GroupBy(x => x.Key, x => x.Value)
        .ToDictionary(x => x.Key, x => (Func<double, double, double>)((k, v) => x.Sum(z => z(k, v))));

      var marketJumpPayment = products.SelectMany(x => x.MarketJumpPayment).GroupBy(x => x.Key, x => x.Value.ToList())
        .ToDictionary(x => x.Key, x => x.SelectMany(z => z).GroupBy(z => z.Key, z => z.Value)
          .ToDictionary(y => y.Key, y => (Func<double, double, double>)((k, v) => y.Sum(z => z(k, v)))));

      return new Product(technicalContinousPayment, technicalJumpPayment, marketContinousPayment, marketJumpPayment,
        ProductCollection.SumOfProducts);
    }

    public static bool TransitionExists<T>(Dictionary<State, Dictionary<State, T>> dicToTest, State fromState, State toState)
    {
      if (dicToTest.ContainsKey(fromState))
        if (dicToTest[fromState].ContainsKey(toState))
          return true;

      return false;
    }

    public static State ConvertToStandardState(State state)
    {
      switch (state)
      {
        case State.FreePolicyActive:
          return State.Active;

        case State.FreePolicyDisabled:
          return State.Disabled;

        case State.FreePolicyDead:
          return State.Dead;

        case State.FreePolicySurrender:
          return State.Surrender;

        default:
          throw new InvalidOperationException("Argument is not a not a free policy state");
      }
    }

    public static List<State> GiveCollectionOfStates(StateCollection collection)
    {
      switch (collection)
      {
        case StateCollection.Standard:
          return new List<State> { State.Active, State.Dead, State.Disabled };

        case StateCollection.StandardWithSurrender:
          return new List<State> { State.Active, State.Dead, State.Disabled, State.Surrender };

        case StateCollection.FreePolicyStates:
          return new List<State> { State.FreePolicyActive, State.FreePolicyDead, State.FreePolicyDisabled };

        case StateCollection.FreePolicyStatesWithSurrender:
          return new List<State> { State.FreePolicyActive, State.FreePolicyDead, State.FreePolicyDisabled,
            State.FreePolicySurrender };

        case StateCollection.RhoModifiedFromStates:
          return new List<State> { State.Active, State.Disabled };

        case StateCollection.AllStates:
          return new List<State> { State.Active, State.Disabled, State.Dead, State.Surrender, State.FreePolicyActive,
            State.FreePolicyDead, State.FreePolicyDisabled, State.FreePolicySurrender };

        default:
          throw new InvalidOperationException("Argument is not valid");
      }
    }
  }
}
