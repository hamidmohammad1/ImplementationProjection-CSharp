using System;
using System.Collections.Generic;

namespace ProjectionSemiMarkov
{
  public class Product
  {
    public Dictionary<State, Func<double, double>> TechnicalContinuousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double>>> TechnicalJumpPayment { get; }

    public Dictionary<State, Func<double, double, double>> MarketContinuousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double, double>>> MarketJumpPayment { get; }

    public Product(
      Dictionary<State, Func<double, double>> technicalContinuousPayment,
      Dictionary<State, Dictionary<State, Func<double, double>>> technicalJumpPayment,
      Dictionary<State, Func<double, double, double>> marketContinuousPayment,
      Dictionary<State, Dictionary<State, Func<double, double, double>>> marketJumpPayment)
    {
      this.TechnicalContinuousPayment = technicalContinuousPayment;
      this.TechnicalJumpPayment = technicalJumpPayment;
      this.MarketContinuousPayment = marketContinuousPayment;
      this.MarketJumpPayment = marketJumpPayment;
    }
  }
}
