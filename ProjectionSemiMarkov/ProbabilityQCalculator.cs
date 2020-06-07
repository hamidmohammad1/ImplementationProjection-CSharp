using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectionSemiMarkov
{
  public class ProbabilityQCalculator : Calculator
  {
    /// <summary>
    /// A dictionary indexed on <see cref="Policy.policyId"/> and contains the semi-markov Probabilities.
    /// </summary>
    /// <remarks>
    /// Given a <c>policyId</c>, a <c>state</c>, a <c>timePoint</c>, a <c>duration</c>, then
    /// Probabilities[policyId][state][timePoint][duration] is the probability
    /// p_{z0,state}(initialTime, (timePoint - 1)*stepSize, initialDuration, (duration - 1)*stepSize)
    /// The z0, initialTime and initialDuration is found on the policy, see mapping <see cref="policies"/>.
    /// </remarks>
    public Dictionary<string, Dictionary<State, double[][]>> Probabilities { get; private set; }

    public Dictionary<string, (State, double)> PolicyIdInitialStateDuration { get; private set; }

    public double Time { get; private set; }
    public Dictionary<double,double> rStar { get; private set; } 
    public Dictionary<double,double> r { get; private set; }
    public double pi1 { get; private set; } 
    public double pi2 { get; private set; }
    Dictionary<string, Dictionary<State, double[][]>> probabilities { get;}
    Dictionary<string, Dictionary<(PaymentStream, Sign), Dictionary<State, double[]>>> technicalreserves { get;}

    /// <summary>
    /// A dictionary containing the market intensities.
    /// </summary>
    protected Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> marketIntensities
      = Setup.CreateMarketBasisIntensities();

    /// <summary>
    /// A dictionary containing the technical intensities.
    /// </summary>
    protected Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> technicalIntensities
      = Setup.CreateTechnicalBasisIntensities().Item1;

    /// <summary>
    /// Constructing ProbabilityCalculator.
    /// </summary>
    public ProbabilityQCalculator(
      //Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> intensities,
      Dictionary<string, Policy> policies,
      Dictionary<string, (State, double)> policyIdInitialStateDuration,
      double time,
      Dictionary<string, Dictionary<State, double[][]>> probabilities,
      Dictionary<string, Dictionary<State, double[]>> technicalReserves
      )
    {
      // Deducing the state space from the possible transitions in intensity dictionary
      var allPossibleTransitions = marketIntensities[Gender.Female].Union(marketIntensities[Gender.Male]).ToList();
      stateSpace = allPossibleTransitions.SelectMany(x => x.Value.Keys)
      .Union(allPossibleTransitions.Select(y => y.Key)).Distinct();

      //todo - is imported correctly!!?
      this.marketIntensities = marketIntensities;
      this.technicalIntensities = technicalIntensities;
      this.policies = policies;
      this.PolicyIdInitialStateDuration = policyIdInitialStateDuration;
      this.Time = time;

      //todo - import!! (need r and r^* as input, as well as pi_1 and pi_2)
      this.rStar = rStar;
      this.r = r;
      this.pi1 = pi1;
      this.pi2 = pi2;

      this.Probabilities = probabilities;
      this.TechnicalReserves = technicalReserves;
    }

    /// <summary>
    /// Allocating memory for matrices inside <see cref="Probabilities"/>.
    /// </summary>
    private void AllocateMemoryAndInitialize()
    {
      QProbabilities = new Dictionary<string, Dictionary<State, double[][]>>();

      foreach (var (policyId, v) in policies)
      {
        var (initialState, initialDuration) = PolicyIdInitialStateDuration[policyId];
        var numberOfTimePoints = GetNumberOfTimePoints(v, Time);
        var stateProbabilities = new Dictionary<State, double[][]>();

        foreach (var state in stateSpace)
        {
          var arrayOfArray = new double[numberOfTimePoints][];

          for (var timeIndex = 0; timeIndex < numberOfTimePoints; timeIndex++)
            arrayOfArray[timeIndex] =
              new double[DurationSupportIndex(initialDuration, timeIndex) + 1];

          // Default values of array elements are zero, so we only set probability for last duration to one.
          arrayOfArray[0][arrayOfArray[0].Length - 1] = state == initialState ? 1 : 0;

          stateProbabilities.Add(state, arrayOfArray);
        }
        Probabilities.Add(policyId, stateProbabilities);
      }
    }

    public Dictionary<string, Dictionary<State, double[][]>> Calculate()
    {
      AllocateMemoryAndInitialize();

      Parallel.ForEach(policies, policy => ProbabilityQCalculatePerPolicy(policy.Value));

      return (Probabilities);
    }

    public void ProbabilityQCalculatePerPolicy(Policy policy)
    {
      var policyProbabilities = probabilities[policy.policyId];
      var policyQProbabilities = Probabilities[policy.policyId];
      var numberOfTimePoints = policyQProbabilities.First().Value.Length;
      var genderMarketIntensity = marketIntensities[policy.gender];
      var genderTechnicalIntensity = technicalIntensities[policy.gender];

      var technicalDaggerReserves = new Dictionary<State,double[][]>(); //todo: import!!
      var dividendsCont = new Dictionary<ContinousDividend,Dictionary<State,double[][]>>(); //new Dictionary<DividendType,Dictionary<State,Func<double[],double[],Dictionary<State,double[][]>,Dictionary<State,Dictionary<State,double[][]>>,Dictionary<State,Dictionary<State,double[][]>>, Dictionary<State,double[][]>,Dictionary<State,double[][]>,Dictionary<State,double[][]>, Dictionary<State,double[][]>>>>(); //todo - import!! //r , r* , V^circ,* , mu_^* , mu , b^circ , b^dagger , V^dagger   
      var dividendsJump = new Dictionary<JumpDividend,Dictionary<State,Dictionary<State,double[][]>>>(); //todo - import!! 

      // Loop over each Time point
      for (var t = 1; t < numberOfTimePoints; t++)
      {
        var durationMaxIndexCur = DurationSupportIndex(policy.initialDuration, t);
        var durationMaxIndexPrev = DurationSupportIndex(policy.initialDuration, t - 1);

        var probIntegrals = new double[durationMaxIndexCur + 1];

        // Loop over j in p_{z0,j}(...)
        foreach (var j in stateSpace)
        {
          // Loop over l in Kolmogorov forward integro-differential equations (Prob. mass going in)
          foreach (var l in genderMarketIntensity.Keys)
          {
            Func<double, double, double> intensity;
            if (!genderMarketIntensity[l].TryGetValue(j, out intensity))
              continue;

            probIntegrals[1] = 0;
            // Riemann sum over duration for integrals to max(u_0+t,D(t))=u_0+t
            if (l == j) //todo - somewhat ugly to case here as we have to write two loops, but should be more optimized
            {
              for (var u = 1; u <= durationMaxIndexPrev; u++)
              {
                probIntegrals[u + 1] = probIntegrals[u] 
                  +(policyProbabilities[l][t - 1][u] - policyProbabilities[l][t - 1][u - 1])*
                  dividendsCont[ContinousDividend.Continuous0][j][t-1][u]/technicalDaggerReserves[j][t-1][u]*stepSize //todo - should probably have linearly-interpolated between durations so we could use u-0.5
                  +(policyQProbabilities[l][t - 1][u] - policyQProbabilities[l][t - 1][u - 1])*
                  dividendsCont[ContinousDividend.Continuous1][j][t-1][u]/technicalDaggerReserves[j][t-1][u]*stepSize
                  -(policyQProbabilities[l][t - 1][u] - policyQProbabilities[l][t - 1][u - 1])*genderMarketIntensity[j][j](policy.age + Time + IndexToTime(t - 0.5),
                  policy.initialDuration + IndexToTime(u - 0.5)) * stepSize;

                //todo: Should probably not have Time in policy. Could consider splitting age and Time to allow for more general intesities.
              }
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexPrev; u++)
              {
                probIntegrals[u + 1] = probIntegrals[u] 
                  +(policyProbabilities[l][t - 1][u] - policyProbabilities[l][t - 1][u - 1])*
                  dividendsJump[JumpDividend.Jump0][l][j][t-1][u]/technicalDaggerReserves[j][t-1][0]*genderMarketIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                  policy.initialDuration + IndexToTime(u - 0.5))*stepSize //todo - should probably have linearly-interpolated between durations so we could use u-0.5
                  +(policyQProbabilities[l][t - 1][u] - policyQProbabilities[l][t - 1][u - 1])*
                  dividendsJump[JumpDividend.Jump1][l][j][t-1][u]/technicalDaggerReserves[j][t-1][0]*genderMarketIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                  policy.initialDuration + IndexToTime(u - 0.5))*stepSize
                  +(policyQProbabilities[l][t - 1][u] - policyQProbabilities[l][t - 1][u - 1])*genderMarketIntensity[l][j](policy.age + Time + IndexToTime(t - 0.5),
                  policy.initialDuration + IndexToTime(u - 0.5))*stepSize;
                //todo: Should probably not have Time in policy. Could consider splitting age and Time to allow for more general intesities.
              }
            }

            // For j = l, the upper limit of the integral is D(t)=d+t
            // For j \neq l, the upper limit is u_0+t which is d-independent
            if (j == l)
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyQProbabilities[j][t][u] = policyQProbabilities[j][t][u] + probIntegrals[u];
            }
            else
            {
              for (var u = 1; u <= durationMaxIndexCur; u++)
                policyQProbabilities[j][t][u] = policyQProbabilities[j][t][u] + probIntegrals.Last();
            }
          }

          // We add the previous probability p_ij(t_0,s,u,d+s-t_0) to get p_ij(t_0,s+h,u,d+s+h-t_0)= p_ij(t_0,s,u,d+s-t_0)+d/ds p_ij(t_0,s,u,d+s-t_0)*h
          for (var u = 1; u <= durationMaxIndexCur; u++)
            policyQProbabilities[j][t][u] = policyQProbabilities[j][t][u] + policyQProbabilities[j][t - 1][u - 1];
        }
      }
    }

  }
}