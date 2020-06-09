using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ProjectionSemiMarkov.HelperFunctions;

namespace ProjectionSemiMarkov
{
  public class ProbabilityCalculator : Calculator
  {
    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains the semi-markov ordinary probabilities.
    /// </summary>
    /// <remarks>
    /// Given a <c>policyId</c>, a <c>state</c>, a <c>timePoint</c>, a <c>duration</c>, then
    /// Probabilities[policyId][state][timePoint][duration] is the probability
    /// p_{z0,state}(initialTime, (timePoint - 1)*stepSize, initialDuration, (duration - 1)*stepSize)
    /// The z0, initialTime and initialDuration is found on the policy, see mapping <see cref="policies"/>.
    /// </remarks>
    public Dictionary<string, Dictionary<State, double[][]>> Probabilities { get; private set; }

    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains the semi-markov rho-modified probabilities.
    /// </summary>
    public Dictionary<string, Dictionary<State, double[][]>> RhoProbabilities { get; private set; }

    /// <summary>
    /// The start time.
    /// </summary>
    public double Time { get; private set; }

    /// <summary>
    /// The free policy factor.
    /// </summary>
    public readonly Dictionary<string, double[]> FreePolicyFactor;

    /// <summary>
    /// The original technical reserves.
    /// </summary>
    public readonly Dictionary<string, Dictionary<State, List<double>>> OriginalTechReserves;

    /// <summary>
    /// The original positive technical reserves.
    /// </summary>
    public readonly Dictionary<string, Dictionary<State, List<double>>> OriginalPositiveTechReserves;

    /// <summary>
    /// The original bonus technical reserves.
    /// </summary>
    public readonly Dictionary<string, Dictionary<State, double[]>> BonusTechReserves;

    /// <summary>
    /// Constructing ProbabilityCalculator.
    /// </summary>
    public ProbabilityCalculator(
      double time,
      Dictionary<string, double[]> freePolicyFactor,
      Dictionary<string, Dictionary<State, List<double>>> originalTechReserves,
      Dictionary<string, Dictionary<State, List<double>>> originalPositiveTechReserves,
      Dictionary<string, Dictionary<State, double[]>> bonusTechReserves)
    {
      this.Time = time;
      this.FreePolicyFactor = freePolicyFactor;
      this.OriginalTechReserves = originalTechReserves;
      this.OriginalPositiveTechReserves = originalPositiveTechReserves;
      this.BonusTechReserves = bonusTechReserves;
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="Probabilities"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize(bool calculateRhoProbability)
    {
      Probabilities = new Dictionary<string, Dictionary<State, double[][]>>();
      AllocateMemoryAndInitializePerDictionary(Probabilities, false);

      if (calculateRhoProbability)
      {
        RhoProbabilities = new Dictionary<string, Dictionary<State, double[][]>>();
        AllocateMemoryAndInitializePerDictionary(RhoProbabilities, true);
      }
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="dic"/>.
    /// </summary>
    private void AllocateMemoryAndInitializePerDictionary(
      Dictionary<string, Dictionary<State, double[][]>> dic,
      bool calculateRhoProbability)
    {
      var statesWhereRhoModifiedIsValid = new List<State> { State.Active, State.Disabled };

      foreach (var (policyId, v) in policies)
      {
        var (initialState, initialDuration) = PolicyIdInitialStateDuration[policyId];

        if (!statesWhereRhoModifiedIsValid.Contains(initialState) && calculateRhoProbability)
          throw new Exception($"Rho-Modified transition do not make sense in {initialState}");

        var numberOfTimePoints = GetNumberOfTimePoints(v, Time);
        var stateProbabilities = new Dictionary<State, double[][]>();

        var statesToCreateEntryFor = calculateRhoProbability
          ? GiveCollectionOfStates(StateCollection.FreePolicyStatesWithSurrender)
          : MarketStateSpace;

        foreach (var state in statesToCreateEntryFor)
        {
          var arrayOfArray = new double[numberOfTimePoints][];

          for (var timeIndex = 0; timeIndex < numberOfTimePoints; timeIndex++)
            arrayOfArray[timeIndex] =
              new double[DurationSupportIndex(initialDuration, timeIndex) + 1];

          // Default values of array elements are zero, so we only set probability for last duration to one.
          var boundary = calculateRhoProbability ? 0.0 : 1;
          arrayOfArray[0][arrayOfArray[0].Length - 1] = state == initialState ? boundary : 0;

          stateProbabilities.Add(state, arrayOfArray);
        }
        dic.Add(policyId, stateProbabilities);
      }
    }

    public Dictionary<string, Dictionary<State, double[][]>> Calculate(bool calculateRhoProbability)
    {
      AllocateMemoryAndInitialize(calculateRhoProbability);
      Parallel.ForEach(policies, policy => ProbabilityCalculatePerPolicy(policy.Value, calculateRhoProbability));

      return (Probabilities);
    }

    private void ProbabilityCalculatePerPolicy(Policy policy, bool calculateRhoProbability)
    {
      ProbabilityCalculatePerPolicy(policy, Probabilities[policy.policyId], false);

      if (calculateRhoProbability)
        ProbabilityCalculatePerPolicy(policy, RhoProbabilities[policy.policyId], true);
    }

    /// <remarks>
    /// Approximation of lebesgue integral
    /// int_0^t f(s) p(.., ds)
    /// \approx sum_i P( s_i < Z < s_(i+1)) f((s_(i+1)-s_i)/2)
    /// = sum_i (p(.., s_(i+1)) - p(.., s_i)) * f((s_(i+1)-s_i)/2)
    /// </remarks>
    private void ProbabilityCalculatePerPolicy(
      Policy policy,
      IReadOnlyDictionary<State, double[][]> prob,
      bool calculateRhoProbability)
    {
      var numberOfTimePoints = prob.First().Value.Length;
      var genderIntensity = marketIntensities[policy.gender];
      var (initialState, initialDuration) = PolicyIdInitialStateDuration[policy.policyId];

      var states = calculateRhoProbability
        ? GiveCollectionOfStates(StateCollection.FreePolicyStatesWithSurrender)
        : MarketStateSpace.ToList();
      var possibleMidStates = genderIntensity.Keys.Where(x => states.Contains(x));

      // Loop over each Time point
      for (var t = 1; t < numberOfTimePoints; t++)
      {
        var durationMaxIndexCur = DurationSupportIndex(initialDuration, t);
        var durationMaxIndexPrev = DurationSupportIndex(initialDuration, t - 1);

        var probIntegrals = new double[durationMaxIndexCur + 1];
        var sumRho = 0.0;

        // Loop over j in p_{z0,j}(...)
        foreach (var j in states)
        {
          // Handling of j = FreePolicyActive in Rho-Modified probabilities
          if (j == State.FreePolicyActive && calculateRhoProbability)
          {
            // Using a DurationSupportIndex, when the function name makes no sense here. The return value is correct one.
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              sumRho += genderIntensity[State.Active][State.FreePolicyActive](policy.age + Time + IndexToTime(t - 0.5), initialDuration + IndexToTime(u - 0.5))
                * (Probabilities[policy.policyId][initialState][t][u] - Probabilities[policy.policyId][initialState][t][u - 1]);
            }

            var midPointFreePolicyFactor = (FreePolicyFactor[policy.policyId][DurationSupportIndex(Time, t)]
              + FreePolicyFactor[policy.policyId][DurationSupportIndex(Time, t - 1)]) / 2;
            sumRho = sumRho * stepSize * midPointFreePolicyFactor;
          }

          // Loop over l in Kolmogorov forward integro-differential equations (Prob. mass going in)
          foreach (var l in possibleMidStates)
          {
            Func<double, double, double> intensity;
            if (!genderIntensity[l].TryGetValue(j, out intensity))
              continue;

            probIntegrals[1] = 0;
            // Riemann sum over duration for integrals
            for (var u = 1; u <= durationMaxIndexPrev; u++)
            {
              probIntegrals[u + 1] = probIntegrals[u] + (prob[l][t - 1][u] - prob[l][t - 1][u - 1])
                * genderIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                  initialDuration + IndexToTime(u - 0.5)) * stepSize;

              //todo: Should probably not have age in policy. Could consider splitting age and Time to allow for more general intesities.
            }

            // For j = l, we need to subtract the cumulative integral for each duration
            // For j \neq l, we need to add the whole integral from 0 to D(s) for each duration
            if (j == l)
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                prob[j][t][u] -= probIntegrals[u];
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                prob[j][t][u] += probIntegrals.Last();
            }
          }

          // We add the previous probability p_ij(t_0,s,u,d+s-t_0) and (1_(j = J + 1) int ... ) to get
          // p_ij(t_0,s+h,u,d+s+h-t_0)= p_ij(t_0,s,u,d+s-t_0) + d/ds p_ij(t_0,s,u,d+s-t_0) * h
          for (var u = 1; u <= durationMaxIndexCur; u++)
            prob[j][t][u] = prob[j][t][u] + sumRho + prob[j][t - 1][u - 1];
        }
      }
    }

    /// <summary>
    /// Calculating market original cash flows
    /// </summary>
    public Dictionary<string, double[]> CalculateMarketOriginalCashFlows()
    {
      var standardStates = GiveCollectionOfStates(StateCollection.Standard);
      var freePolicyStates = GiveCollectionOfStates(StateCollection.FreePolicyStates);
      var policyIdCashFlows = new Dictionary<string, double[]>();

      foreach (var (policyId, policy) in policies)
      {
        var cashFlowsStandardStates = new double[Probabilities[policyId].First().Value.Length];
        var cashFlowsFreePolicyStates = new double[Probabilities[policyId].First().Value.Length];

        var positiveOriginalPayments = policy.Payments[(PaymentStream.Original, Sign.Positive)];
        var negativeOriginalPayments = policy.Payments[(PaymentStream.Original, Sign.Negative)];
        var sumPayments = SumProducts(new List<Product> { positiveOriginalPayments, negativeOriginalPayments });

        for(var z = 0; z < 90; z++)
          Console.WriteLine(positiveOriginalPayments.MarketContinuousPayment[State.Disabled](z,2));

        // Since we have no products with jump payments (except surrender value), we only extract the continuous payments.
        var policyIdOriginalTechReserve = OriginalTechReserves[policyId];
        CalculateCashFlowsPerStateSet(standardStates, cashFlowsStandardStates, sumPayments.MarketContinuousPayment,
          policyIdOriginalTechReserve, policy.age, marketIntensities[policy.gender], Probabilities[policyId]);

        var policyIdOriginalPositiveTechReserve = OriginalPositiveTechReserves[policyId];
        CalculateCashFlowsPerStateSet(freePolicyStates, cashFlowsFreePolicyStates, positiveOriginalPayments.MarketContinuousPayment,
          policyIdOriginalPositiveTechReserve, policy.age, marketIntensities[policy.gender], RhoProbabilities[policyId]);

        var s = cashFlowsFreePolicyStates.Zip(cashFlowsStandardStates, (x, y) => x + y).ToArray();
        for (var j = 1; j < s.Length; j++)
          s[j] += s[j - 1];

        policyIdCashFlows.Add(policyId, s);
      }

      return policyIdCashFlows;
    }

    /// <summary>
    /// Calculating market bonus cash flows
    /// </summary>
    public Dictionary<string, double[]> CalculateMarketBonusCashFlows()
    {
      var allStates = GiveCollectionOfStates(StateCollection.AllStates);
      var policyIdCashFlows = new Dictionary<string, double[]>();

      foreach (var (policyId, policy) in policies)
      {
        var cashFlows = new double[Probabilities[policyId].First().Value.Length];
        var bonusPayments = policy.Payments[(PaymentStream.Bonus, Sign.Positive)];

        // Since we have no products with jump payments (except surrender value), we only extract the continuous payments.
        var policyIdBonusTech = BonusTechReserves[policyId].ToDictionary(x => x.Key, x => x.Value.ToList());
        CalculateCashFlowsPerStateSet(allStates, cashFlows, bonusPayments.MarketContinuousPayment,
          policyIdBonusTech, policy.age, marketIntensities[policy.gender], Probabilities[policyId]);

        for (var j = 1; j < cashFlows.Length; j++)
          cashFlows[j] += cashFlows[j - 1];

        policyIdCashFlows.Add(policyId, cashFlows);
      }

      return policyIdCashFlows;
    }

    /// <summary>
    /// Calculating market cash flows for a state set.
    /// </summary>
    public void CalculateCashFlowsPerStateSet(
      List<State> states,
      double[] cashFlows,
      Dictionary<State, Func<double, double, double>>  contPayments,
      Dictionary<State, List<double>> techReserve,
      double policyAge,
      Dictionary<State, Dictionary<State, Func<double, double, double>>> genderIntensity,
      Dictionary<State, double[][]> policyIdProb)
    {
      // We should always have active and free policy active, since there are surrender payment upon transition
      foreach (var toState in states.Where(x => contPayments.Keys.Contains(x) || x == State.FreePolicyActive || x == State.Active))
      {
        var paymentInState = contPayments.ContainsKey(toState) ? contPayments[toState] : (x, y) => 0.0;
        var policyIdProbInState = policyIdProb[toState];

        for (var s = 1; s < cashFlows.Length; s++)
        {
          var cashFlow = 0.0;

          if (TransitionExists(genderIntensity, toState, State.Surrender))
          {
            for (var z = 1; z < policyIdProbInState[s].Length; z++)
              cashFlow = cashFlow + (paymentInState(policyAge + (s - 0.5) * stepSize, (z - 0.5) * stepSize)
                + techReserve[toState][s] * genderIntensity[toState][State.Surrender](policyAge + (s - 0.5) * stepSize, (z - 0.5) * stepSize))
                * (policyIdProbInState[s][z] - policyIdProbInState[s][z - 1]);
          }
          else if (TransitionExists(genderIntensity, toState, State.FreePolicySurrender))
          {
            for (var z = 1; z < policyIdProbInState[s].Length; z++)
              cashFlow = cashFlow + (paymentInState(policyAge + (s - 0.5) * stepSize, (z - 0.5) * stepSize)
                + techReserve[ConvertToStandardState(toState)][s]
                * genderIntensity[toState][State.FreePolicySurrender](policyAge + (s - 0.5) * stepSize, (z - 0.5) * stepSize))
                * (policyIdProbInState[s][z] - policyIdProbInState[s][z - 1]);
          }
          else
          {
            for (var z = 1; z < policyIdProbInState[s].Length; z++)
              cashFlow = cashFlow + paymentInState(policyAge + (s - 0.5) * stepSize, (z - 0.5) * stepSize)
                * (policyIdProbInState[s][z] - policyIdProbInState[s][z - 1]);
          }

          cashFlows[s] = cashFlows[s] + cashFlow * stepSize;
        }
      }
    }
  }
}