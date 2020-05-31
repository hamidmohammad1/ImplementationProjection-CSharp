using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace ProjectionSemiMarkov
{
  class Program
  {
    static void Main(string[] args)
    {
      var stopWatch = new Stopwatch();
      stopWatch.Start();

      var marketBasis = Setup.CreateMarketBasisIntensities();
      var technicalBasis = Setup.CreateTechnicalBasisIntensities();
      var policies = Setup.CreatePolicies();

      var policyIdInitialStateDuration =
        policies.ToDictionary(x => x.Key, x => (x.Value.initialState, x.Value.initialDuration));
      var time = 0.0;

      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies, policyIdInitialStateDuration, time);
      var marketProb = marketProbabilityCalculator.Calculate();

      var technicalReserveCalculator = new TechnicalReserveCalculator(technicalBasis.Item1, technicalBasis.Item2, policies);
      var techReserves = technicalReserveCalculator.Calculate();

      // Calculating \rho = (V_{Active}^{\circ,*,+} + V_{Active}^{\circ,*,-})/ V_{Active}^{\circ,*,+} for each Time point
      var freePolicyFactor = techReserves
        .ToDictionary(policy => policy.Key,
          policy => policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active]
            .Zip(policy.Value[(PaymentStream.Original, Sign.Negative)][State.Active], (x, y) => x + y)
            .Zip(policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active], (x, y) => x / y));

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
