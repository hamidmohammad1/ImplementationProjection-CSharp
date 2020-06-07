using System;
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

      var technicalReserveCalculator = new TechnicalReserveCalculator();
      var techReserves = technicalReserveCalculator.Calculate();

      // Calculating \rho = (V_{Active}^{\circ,*,+} + V_{Active}^{\circ,*,-})/ V_{Active}^{\circ,*,+} for each Time point
      var freePolicyFactor = techReserves
        .ToDictionary(policy => policy.Key,
          policy => policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active]
            .Zip(policy.Value[(PaymentStream.Original, Sign.Negative)][State.Active], (x, y) => x + y)
            .Zip(policy.Value[(PaymentStream.Original, Sign.Positive)][State.Active], (x, y) => y == 0 ? 1.0 : x / y)
            .ToArray());

      var policies = Setup.CreatePolicies();
      var policyIdInitialStateDuration =
        policies.ToDictionary(x => x.Key, x => (x.Value.initialState, x.Value.initialDuration));
      var time = 0.0;
      var marketProbabilityCalculator = new ProbabilityCalculator(policyIdInitialStateDuration, time, freePolicyFactor);

      // Initial state must be active or disability, if one wants to calculate RhoModifiedProbabilities
      var marketProbRho = marketProbabilityCalculator.Calculate(calculateRhoProbability: true);
      var marketProb = marketProbabilityCalculator.Calculate(calculateRhoProbability: false);

      var marketProbabilityQCalculator = new ProbabilityQCalculator(policies, policyIdInitialStateDuration, time,marketProb,techReserves);
      var marketProbQ = marketProbabilityQCalculator.Calculate();

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
