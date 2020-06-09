using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static ProjectionSemiMarkov.HelperFunctions;

using Math = System.Math;

namespace ProjectionSemiMarkov
{
  class StateIndependentProjection
  {
       protected Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> marketIntensities //todo - should have imported
      = Setup.CreateMarketBasisIntensities();

    /// <summary>
    /// A dictionary containing the technical intensities.
    /// </summary>
    protected Dictionary<Gender, Dictionary<State, Dictionary<State, Func<double, double, double>>>> technicalIntensities //todo - should have imported
      = Setup.CreateTechnicalBasisIntensities().Item1;

    /// <summary>
    /// A list of possible states in market basis.
    /// </summary>
    public IEnumerable<State> MarketStateSpace => GiveStateSpaceFromIntensities(marketIntensities); //todo - should have imported
    
    /// <summary>
    /// The projection Input
    /// </summary>
    public ProjectionInput Input { get; private set; }

    /// <summary>
    /// The economic scenario generator Input
    /// </summary>
    public EconomicScenarioGenerator EcoScenarioGenerator { get; private set; }

    /// <summary>
    /// The number of economic scenarios.
    /// </summary>
    public int NumberOfEconomicScenarios { get; private set; }

    /// <summary>
    /// The step size for projection. It must a multiple of <see cref="ProjectionInput.StepSize"/>.
    /// </summary>
    public double ProjectionStepSize = 1;

    /// <summary>
    /// The end time.
    /// </summary>
    public int ProjectionEndTime => (int)EcoScenarioGenerator.LastExpiryTime;

    /// <summary>
    /// The end time.
    /// </summary>
    public int NumberOfProjectionTimes => (int)(ProjectionEndTime / ProjectionStepSize + 1);

    /// <summary>
    /// The projection result indexed on economic scenario.
    /// </summary>
    public List<StateIndependentProjectionResult> ProjectionResult { get; private set; }

    /// <summary>
    /// Constructs a instance of StateIndependentProjection.
    /// </summary>
    public StateIndependentProjection(
      ProjectionInput input,
      EconomicScenarioGenerator ecoScenarioGenerator,
      int numberOfEconomicScenarios)
    {
      Input = input;
      EcoScenarioGenerator = ecoScenarioGenerator;
      NumberOfEconomicScenarios = numberOfEconomicScenarios;
      ProjectionResult = new List<StateIndependentProjectionResult>();
    }

    /// <summary>
    /// Initializing StateIndependentProjectionResult for time zero.
    /// </summary>
    public void Initialize(StateIndependentProjectionResult ecoResult)
    {
      // Equity
      ecoResult.EquityResults.Equity[0] = Math.Pow(10, 7);

      // Portfolio quantities
      foreach (var portfolio in ecoResult.PortfolioResults)
      {
        portfolio.Value.AssetProcess[0] = Math.Pow(10, 8);
        portfolio.Value.ProjectedBonusCashFlow[0] = 0.0;
        portfolio.Value.QProcess[0] = 0.0;
      }
    }

    /// <summary>
    /// Calculating reserve by discounting cash flow with relevant zero coupon bond prices.
    /// </summary>
    public void CalculatePortFolioWideReserveForCurrentTimePoint(StateIndependentProjectionResult ecoResult,
      int timePoint)
    {
      foreach (var (portfolio, value) in ecoResult.PortfolioResults)
      {
        value.PortfolioWideTechnicalReserve[timePoint] = Input.PortfolioWideOriginalTechReserves[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint)]
          + value.QProcess[timePoint]* Input.PortfolioWideBonusTechReserves[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint)];

        value.PortfolioWideMarketReserve[timePoint] =
          ReserveCalculator(
          timePoint,
          ecoResult.EconomicScenario[Assets.ShortRate][ProjectionIndexCalculatorIndexConverter(timePoint)],
          Input.MarketOriginalCashFlows[portfolio]
            .Zip(Input.MarketBonusCashFlows[portfolio], (x, y) => x + value.QProcess[timePoint] * y).ToArray());
      }
    }

    /// <summary>
    /// Calculate controls for current time point.
    /// </summary>
    private void CalculateControlsForCurrentTimePoint(StateIndependentProjectionResult ecoResult, int timePoint)
    {
      //TODO OLIVER
      foreach (var portfolio in ecoResult.PortfolioResults)
      {

        var x = 0.0;
        if (timePoint % 12 == 0)
        {
          if (portfolio.Value.AssetProcess[timePoint] - portfolio.Value.PortfolioWideTechnicalReserve[timePoint] > 0)
            x = 0.003 * (portfolio.Value.AssetProcess[timePoint] - portfolio.Value.PortfolioWideTechnicalReserve[timePoint]);
          else
          {
            x = -(portfolio.Value.AssetProcess[timePoint]-portfolio.Value.PortfolioWideTechnicalReserve[timePoint]);
          }
        }
        else
        {
          x = 0;
        }

        var h1 = new double(); //Use "Tax- and expense-modified risk-minimization for insurance payment processes" p.24 with gamma=delta=0
        var rt = ecoResult.EconomicScenario[Assets.ShortRate][ProjectionIndexCalculatorIndexConverter(timePoint)]; //todo - unsure if this is the right timePoint conversion here.
        var timePointYear = ProjectionIndexToTimeInYear(timePoint);
        var Y = 0.0;
        for (var t = timePoint; t <= ProjectionTimeInYearToIndex(ProjectionEndTime); t++)
        {
            Y = (Input.MarketOriginalCashFlows[portfolio.Key][t]+portfolio.Value.QProcess[timePoint]*Input.MarketBonusCashFlows[portfolio.Key][t])
                -(Input.MarketOriginalCashFlows[portfolio.Key][timePoint]+portfolio.Value.QProcess[timePoint]*Input.MarketBonusCashFlows[portfolio.Key][timePoint]);

            h1 = h1 + EcoScenarioGenerator.ZeroCouponBondPriceDerivatives(rt,timePointYear,ProjectionIndexToTimeInYear(t))
                      /EcoScenarioGenerator.ZeroCouponBondPriceDerivatives(rt,timePointYear,ProjectionEndTime)
                      *Y
                      *ProjectionStepSize;
        }

        portfolio.Value.TransactionProcess[Index.Zero][timePoint] = 0.0;
        portfolio.Value.TransactionProcess[Index.One][timePoint] = x; //take 0.3% of "surplus" if positive, otherwise, transfer so surplus is non-negative 
        portfolio.Value.DividendProcess[Index.Zero][timePoint] = 0.01;
        portfolio.Value.DividendProcess[Index.One][timePoint] = 0.01;
        portfolio.Value.ShareInRiskyStockAssetProcess[timePoint] = h1;
      }
      ecoResult.EquityResults.ShareInRiskyAssetEquity[timePoint] = 0.0;
    }

    /// <summary>
    /// Projecting shapes to next time point.
    /// </summary>
    public void ProjectShapesNextTimePoint(StateIndependentProjectionResult ecoResult, int timePoint)
    {
      var shortRateToLastTimePoint = ecoResult.EconomicScenario[Assets.ShortRate][ProjectionIndexCalculatorIndexConverter(timePoint - 1)];
      var lastTimePointRiskyAssetPrice = ecoResult.EconomicScenario[Assets.RiskyAsset][ProjectionIndexCalculatorIndexConverter(timePoint - 1)];
      var riskyAssetPriceChange =
        ecoResult.EconomicScenario[Assets.RiskyAsset][ProjectionIndexCalculatorIndexConverter(timePoint)]
        - lastTimePointRiskyAssetPrice;

      var transactionSum = 0.0;
      // Portfolio quantities
      foreach (var (portfolio, value) in ecoResult.PortfolioResults)
      {
        // Projecting Q-process
        value.QProcess[timePoint] = value.QProcess[timePoint - 1]
          + (value.DividendProcess[Index.Zero][timePoint - 1]
             + value.DividendProcess[Index.One][timePoint - 1] * value.QProcess[timePoint - 1]) * ProjectionStepSize;

        value.ProjectedBonusCashFlow[timePoint] = value.ProjectedBonusCashFlow[timePoint - 1]
          + (value.QProcess[timePoint] - value.QProcess[timePoint - 1]) / 2
          * (Input.MarketBonusCashFlows[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint)]
          - Input.MarketBonusCashFlows[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint - 1)]);

        value.AssetProcess[timePoint] = value.AssetProcess[timePoint - 1]
          + shortRateToLastTimePoint * (value.AssetProcess[timePoint - 1]
            - value.ShareInRiskyStockAssetProcess[timePoint - 1] * lastTimePointRiskyAssetPrice) * ProjectionStepSize
          + value.ShareInRiskyStockAssetProcess[timePoint - 1] * riskyAssetPriceChange
          - (Input.MarketOriginalCashFlows[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint)]
            - Input.MarketOriginalCashFlows[portfolio][ProjectionIndexCalculatorIndexConverter(timePoint - 1)])
          - (value.ProjectedBonusCashFlow[timePoint] - value.ProjectedBonusCashFlow[timePoint - 1])
          - value.TransactionProcess[Index.Zero][timePoint - 1] * ProjectionStepSize
          - value.TransactionProcess[Index.One][timePoint - 1];

        transactionSum += value.TransactionProcess[Index.Zero][timePoint - 1] * ProjectionStepSize
          + value.TransactionProcess[Index.One][timePoint - 1];
      }

      // Projecting E-process
      var lastTimeEquity = ecoResult.EquityResults.Equity[timePoint - 1];
      var shareInRiskyAsset = ecoResult.EquityResults.ShareInRiskyAssetEquity[timePoint - 1];

      ecoResult.EquityResults.Equity[timePoint] = lastTimeEquity
        + shortRateToLastTimePoint * (lastTimeEquity
          - shareInRiskyAsset * lastTimePointRiskyAssetPrice) * ProjectionStepSize
        + shareInRiskyAsset * riskyAssetPriceChange + transactionSum;
    }

    /// <summary>
    /// Calculating reserve by discounting cash flow with relevant zero coupon bond prices.
    /// </summary>
    public double ReserveCalculator(int timePoint, double shortRate, double[] cashFlows)
    {
      var reserve = 0.0;

      // We minus with one, cause we are integrating over intervals (t_{i}, t_{i+1}], ..., (t_{n-1}, t_{n}].
      for (var i = timePoint; i < ProjectionIndexCalculatorIndexConverter(cashFlows.Length, true) - 1; i++)
      {
        reserve += EcoScenarioGenerator.ZeroCouponBondPrices(shortRate, ProjectionIndexToTimeInYear(timePoint),
            ProjectionIndexToTimeInYear(i + 0.5))
          * (cashFlows[ProjectionIndexCalculatorIndexConverter(i + 1)] - cashFlows[ProjectionIndexCalculatorIndexConverter(i)])
          * 0.5 * ProjectionStepSize;
      }

      return reserve;
    }

    /// <summary>
    /// Projecting for all economic scenarios.
    /// </summary>
    public Balance Project()
    {
      for (var i = 0; i < NumberOfEconomicScenarios; i++)
      {
        var ecoResult = new StateIndependentProjectionResult(
          Input.Policies.Keys,
          NumberOfProjectionTimes,
          EcoScenarioGenerator.SimulateMarket());

        ProjectPerEconomicScenario(ecoResult);

        ProjectionResult.Add(ecoResult);
      }

      return CalculateBalance();
    }

    /// <summary>
    /// Calculating balance values
    /// </summary>
    public Balance CalculateBalance()
    {
      // The V^b(0) for each portfolio
      var bonusReserveAtTimeZero = Input.Policies.ToDictionary(
        x => x.Key,
        x => ProjectionResult
          .Average(eco =>
            ReserveCalculator(
              timePoint: 0,
              shortRate: eco.EconomicScenario[Assets.ShortRate][0],
              cashFlows: eco.PortfolioResults[x.Key].ProjectedBonusCashFlow)));

      return new Balance(bonusReserveAtTimeZero, ProjectionResult, Input);
    }

    /// <summary>
    /// Projecting for per economic scenarios.
    /// </summary>
    public void ProjectPerEconomicScenario(StateIndependentProjectionResult ecoResult)
    {
      Initialize(ecoResult);

      for (var i = 0; i < NumberOfProjectionTimes - 1; i++)
        ProjectPerTimePoint(i, ecoResult);
    }

    /// <summary>
    /// Projecting for per time point.
    /// </summary>
    public void ProjectPerTimePoint(int timePoint, StateIndependentProjectionResult ecoResult)
    {
      // Calculating for current time
      CalculatePortFolioWideReserveForCurrentTimePoint(ecoResult, timePoint);
      CalculateControlsForCurrentTimePoint(ecoResult, timePoint);

      // Project shaped to next time point
      ProjectShapesNextTimePoint(ecoResult, timePoint + 1);
    }

    /// <summary>
    /// Converts a time point in unit projection step size into units of step size from calculator or reverse
    /// based on a boolean.
    /// </summary>
    public int ProjectionIndexCalculatorIndexConverter(int timePoint, bool reverse = false)
    {
      if (reverse)
        return (int)(timePoint * Input.StepSize);

      return (int)(timePoint / Input.StepSize);
    }

    /// <summary>
    /// Converts a time point in unit projection step size to time in year
    /// </summary>
    public int ProjectionIndexToTimeInYear(double timePoint)
    {
      return (int)(timePoint * ProjectionStepSize);
    }

    public int ProjectionTimeInYearToIndex(double timePoint)
    {
      return (int)(timePoint / ProjectionStepSize);
    }

    public class StateIndependentProjectionResult
    {
      /// <summary>
      /// The results per portfolio
      /// </summary>
      public Dictionary<string, PortfolioResult> PortfolioResults { get; set; }

      /// <summary>
      /// The results for equity
      /// </summary>
      public EquityResult EquityResults { get; set; }

      /// <summary>
      /// The economic scenario, S_0(t), S_1(t)
      /// </summary>
      public Dictionary<Assets, double[]> EconomicScenario { get; set; }

      public StateIndependentProjectionResult(
        IEnumerable<string> policyIds,
        int numberOfProjectionTimes,
        Dictionary<Assets, double[]> economicScenario)
      {
        PortfolioResults = policyIds.ToDictionary(x => x, x => new PortfolioResult(numberOfProjectionTimes));
        EconomicScenario = economicScenario;
        EquityResults = new EquityResult(numberOfProjectionTimes);
      }
    }

    public class EquityResult
    {
      /// <summary>
      /// The equity, E(t)
      /// </summary>
      public double[] Equity { get; set; }

      /// <summary>
      /// Investment portfolio for equity, \eta^E(t)
      /// </summary>
      public double[] ShareInRiskyAssetEquity { get; set; }

      public EquityResult(int numberOfProjectionTimes)
      {
        Equity = new double[numberOfProjectionTimes];
        ShareInRiskyAssetEquity = new double[numberOfProjectionTimes];
      }
    }

    /// <summary>
    /// The class containing balance values.
    /// </summary>
    public class Balance
    {
      /// <summary>
      /// The V^b(0) per portfolio
      /// </summary>
      public Dictionary<string, double> BonusReservePerPortfolioAtTimeZero { get; set; }

      /// <summary>
      /// The projection results
      /// </summary>
      public List<StateIndependentProjectionResult> ProjectionResults { get; set; }

      /// <summary>
      /// The projection input
      /// </summary>
      public ProjectionInput ProjectionInput { get; set; }

      public Balance(Dictionary<string, double> bonusReservePerPortfolio,
        List<StateIndependentProjectionResult> projectionResults,
        ProjectionInput projectionInput)
      {
        this.BonusReservePerPortfolioAtTimeZero = bonusReservePerPortfolio;
        this.ProjectionResults = projectionResults;
        this.ProjectionInput = projectionInput;
      }
    }

    /// <summary>
    /// The class containing results of projection per economic scenario
    /// </summary>
    public class PortfolioResult
    {
      /// <summary>
      /// The portfolio wide technical reserves (Savings account, X(t))
      /// </summary>
      public double[] PortfolioWideTechnicalReserve { get; set; }

      /// <summary>
      /// The portfolio wide market reserves (Guaranteed benefits, GY(t))
      /// </summary>
      public double[] PortfolioWideMarketReserve { get; set; }

      /// <summary>
      /// The dividend process, \widetilde{\delta}^0(t) and \widetilde{\delta}^1(t)
      /// </summary>
      public Dictionary<Index, double[]> DividendProcess { get; set; }

      /// <summary>
      /// The Q-process, Q(t) per policy
      /// </summary>
      public double[] QProcess { get; set; }

      /// <summary>
      /// The projection bonus cash flow, A^b(0, ds)
      /// </summary>
      public double[] ProjectedBonusCashFlow { get; set; }

      /// <summary>
      /// The asset process, U_S(t)
      /// </summary>
      public double[] AssetProcess { get; set; }

      /// <summary>
      /// Investment portfolio for asset process, \eta^U(t)
      /// </summary>
      public double[] ShareInRiskyStockAssetProcess { get; set; }

      /// <summary>
      /// The transaction process, \xi^0(t) and \xi^1(t)
      /// </summary>
      /// <remarks>
      /// We assume that the jump transaction times is exactly at same projection times.
      /// </remarks>
      public Dictionary<Index, double[]> TransactionProcess { get; set; }

      public PortfolioResult(int numberOfProjectionTimes)
      {
        PortfolioWideTechnicalReserve = new double[numberOfProjectionTimes];
        PortfolioWideMarketReserve = new double[numberOfProjectionTimes];
        QProcess = new double[numberOfProjectionTimes];
        ProjectedBonusCashFlow = new double[numberOfProjectionTimes];
        DividendProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        AssetProcess = new double[numberOfProjectionTimes];
        TransactionProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        ShareInRiskyStockAssetProcess = new double[numberOfProjectionTimes];
      }
    }
  }
}

