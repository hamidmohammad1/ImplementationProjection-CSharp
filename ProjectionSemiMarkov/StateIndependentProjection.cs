using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectionSemiMarkov
{
  class StateIndependentProjection
  {
    /// <summary>
    /// The projection Input
    /// </summary>
    public ProjectionInput Input { get; private set; }

    /// <summary>
    /// The economic scenario
    /// </summary>
    public Dictionary<Assets, double[]> EconomicScenario { get; private set; }

    /// <summary>
    /// A function calculating zero coupon bond prices for r(t), time t and maturity T
    /// </summary>
    public Func<double, double, double, double> ZeroCouponBondPriceFunc { get; private set; }

    /// <summary>
    /// Constructs a instance of StateIndependentProjection.
    /// </summary>
    public StateIndependentProjection(
      ProjectionInput input,
      Dictionary<Assets, double[]> ecoScenario,
      Func<double, double, double, double> zeroCouponBondPriceFunc)
    {
      this.Input = input;
      this.EconomicScenario = ecoScenario;
      this.ZeroCouponBondPriceFunc = zeroCouponBondPriceFunc;
    }

    public void Project()
    {
    }

    public void ProjectPerEconomicScenario()
    {
    }

    public void ProjectPerTimePoint()
    {
      //TODO Calculate mean portfolio

      //TODO Calculate controls

      //TODO Project Q, E, Us

      //TODO Calculate next time bonus cash flow
    }

    public void Result()
    {
    }
  }
}
