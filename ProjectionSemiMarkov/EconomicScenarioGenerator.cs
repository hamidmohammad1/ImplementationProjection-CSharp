using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectionSemiMarkov
{
  class EconomicScenarioGenerator : Calculator
  {
    /// <summary>
    /// The risky asset modelled by a diffusion: dS(t) = mu * S(t) dt + sigma * S(t) dW_1(t).
    /// </summary>
    /// <remarks>
    /// The parameters is coming from S&P index: April 2020: mu = 12,68; May 2020: sigma =  0.2
    /// </remarks>
    private (double initialValue, double μ, double σ) riskyAsset = (100, 0.04, -0.2);

    /// <summary>
    /// The Vasicek modelled by a diffusion: dr(t) = (b - β * r(t))dt +  a * dW_2(t). Old beta: 0.0.162953
    /// </summary>
    private (double initialValue, double b, double β, double a) vasicek = (0.01, 0.007006001, 0.192953, 0.015384);

    /// <summary>
    /// The number of time points. This is number of step to max expiry for all policies.
    /// </summary>
    private Random random = new Random();

    /// <summary>
    /// Random generator.
    /// </summary>
    private int numberOfTimePoints => (int)(LastExpiryTime / stepSize) + 1;

    /// <summary>
    /// Initializing the assets.
    /// </summary>
    public (double[] shortRate, double[] riskyAssets) InitializeAssets()
    {
      var shortRate = new double[numberOfTimePoints];
      shortRate[0] = vasicek.initialValue;

      var riskyAssets = new double[numberOfTimePoints];
      riskyAssets[0] = riskyAsset.initialValue;

      return (shortRate, riskyAssets);
    }

    /// <summary>
    /// Simulating a market path.
    /// </summary>
    public Dictionary<Assets, double[]> SimulateMarket()
    {
      var (shortRate, riskyAssets) = InitializeAssets();

      for (var i = 1; i < numberOfTimePoints; i++)
      {
        riskyAssets[i] = riskyAssets[i - 1] * Math.Exp((riskyAsset.μ - 0.5 * Math.Pow(riskyAsset.σ, 2))
          * stepSize + riskyAsset.σ * Math.Sqrt(stepSize) * random.NextGaussian());

        shortRate[i] = shortRate[i - 1] + (vasicek.b - vasicek.β * shortRate[i - 1])  * stepSize
          + vasicek.a * Math.Sqrt(stepSize) * random.NextGaussian();
      }

      return new Dictionary<Assets, double[]>
        {
          { Assets.ShortRate, shortRate },
          { Assets.RiskyAsset, riskyAssets },
        };
    }

    /// <summary>
    /// Calculating zero coupon bond prices for a given interest r(t), start time t and maturity T.
    /// P(t,T) = A(t,T) * exp(- B(t,T) * r(t)) = E[exp(-int_t^T r(s) ds) \mid r(t)]
    /// </summary>
    /// <remarks>
    /// https://quant.stackexchange.com/questions/34809/zero-coupon-bond-price-volatility-with-one-factor-hull-white-interest-rate-model
    /// We have the model: dr(t) = (b - \beta r(t))dt +  a dW_2(t)
    /// B(t,T) &= \frac{1}{\beta}\cdot (1 -e^{-\beta(T-t)})
    /// A(t,T) &= \exp\left(-b\int_t^TB(u,T) du -\frac{a^2}{2\beta^2}(B(t,T) - (T-t)) -\frac{a^2}{4\beta}B(t,T)^2 \right)
    ///        &= \exp\left(-\frac{ b}{\beta}(T-t- \frac{1}{\beta}(1-e^{-\beta(T-t)}) ) -\frac{a^2}{2\beta^2}(B(t, T) - (T-t)) -\frac{a^2}{4\beta}B(t, T)^2 \right)
    ///        &= \exp\left(-\frac{ b}{\beta}(T-t- B(t, T) ) -\frac{a^2}{2\beta^2}(B(t, T) - (T-t)) -\frac{a^2}{4\beta}B(t, T)^2 \right)
    /// </remarks>
    public double ZeroCouponBondPrices(double interest, double t, double T) //todo - hvis vi er usikre på disse resultater kan vi bruge drengenes parametrisering da de også finder F(t,r(t),T) og dens afledte 
    {
      var remainingTime = T - t;
      var b = 1 / vasicek.β * (1 - Math.Exp(-vasicek.β * remainingTime));
      var a = Math.Exp(-vasicek.b/vasicek.β * (remainingTime - b)
        - Math.Pow(vasicek.a, 2)/(2 * Math.Pow(vasicek.β, 2))*(b - remainingTime)
        - Math.Pow(vasicek.a, 2)/(4 * vasicek.β)* Math.Pow(b, 2));
      return a * Math.Exp(-b * interest);
    }

    public double ZeroCouponBondPriceDerivatives(double interest, double t, double T) 
    {
      var remainingTime = T - t;
      var b = 1 / vasicek.β * (1 - Math.Exp(-vasicek.β * remainingTime));
      var a = Math.Exp(-vasicek.b/vasicek.β * (remainingTime - b)
        - Math.Pow(vasicek.a, 2)/(2 * Math.Pow(vasicek.β, 2))*(b - remainingTime)
        - Math.Pow(vasicek.a, 2)/(4 * vasicek.β)* Math.Pow(b, 2));
      return -b * a * Math.Exp(-b * interest);
    }

  }

  /// <summary>
  /// Some extension methods for <see cref="Random"/> for creating a few more kinds of random stuff.
  /// </summary>
  /// <remarks>
  /// This extension method is taken from https://bitbucket.org/Superbest/superbest-random/src/master/
  /// </remarks>
  public static class RandomExtensions
  {
    /// <summary>
    /// Generates normally distributed numbers. Each operation makes two Gaussians for the price of one,
    /// and apparently they can be cached or something for better performance, but who cares.
    /// </summary>
    /// <param name="r"></param>
    /// <param name = "mu">Mean of the distribution</param>
    /// <param name = "sigma">Standard deviation</param>
    /// <returns></returns>
    public static double NextGaussian(this Random r, double mu = 0, double sigma = 1)
    {
      var u1 = r.NextDouble();
      var u2 = r.NextDouble();

      var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
      var rand_normal = mu + sigma * rand_std_normal;

      return rand_normal;
    }
  }
}
