using System;
using System.Collections.Generic;

namespace ProjectionSemiMarkov
{
  public class Product
  {
    public Dictionary<State, Func<double, double>> technicalContinousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double>>> technicalJumpPayment { get; }

    public Dictionary<State, Func<double, double, double>> marketContinousPayment { get; }

    public Dictionary<State, Dictionary<State, Func<double, double, double>>> marketJumpPayment { get; }

    public Product(
      Dictionary<State, Func<double, double>> technicalContinousPayment,
      Dictionary<State, Dictionary<State, Func<double, double>>> technicalJumpPayment,
      Dictionary<State, Func<double, double, double>> marketContinousPayment,
      Dictionary<State, Dictionary<State, Func<double, double, double>>> marketJumpPayment)
    {
      this.technicalContinousPayment = technicalContinousPayment;
      this.technicalJumpPayment = technicalJumpPayment;
      this.marketContinousPayment = marketContinousPayment;
      this.marketJumpPayment = marketJumpPayment;
    }
  }
}
