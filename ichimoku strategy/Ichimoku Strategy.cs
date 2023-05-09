using System;
using System.ComponentModel;
using ATAS.Indicators.Technical;
using ATAS.Strategies.Chart;
using ATAS.Indicators;
using System.Windows.Media;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Technical.Properties;
using Utils.Common.Localization;
using System.Reflection.Metadata;
using ATAS.DataFeedsCore;
using Utils.Common.Logging;
using System.Runtime.CompilerServices;

public class IchimokuKinkoHyoStrategy : ChartStrategy
{
    
    //Position Checker
    private bool _isInPosition = false;
    


    // Inputs
    [Display(Name = "Enable Alerts")]
    public bool enable_Alert = false;

    
    private bool long_entry = true;

    
    private bool short_entry = true;

    private bool open_Alert = false;
    private bool close_Alert = false;
    private bool bullish_Triggered = false;
    private bool bearish_Triggered = false;

    [Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "DaysLookBack", Order = int.MaxValue, Description = "DaysLookBackDescription")]
    [Range(0, 10000)]
    public int Days
    {
        get
        {
            return _days;
        }
        set
        {
            _days = value;
            RecalculateValues();
        }
    }

    [LocalizedCategory(typeof(Resources), "Settings")]
    [DisplayName("Tenkan-sen")]
    [Range(1, 10000)]
    public int Tenkan
    {
        get
        {
            return _conversionHigh.Period;
        }
        set
        {
            int num3 = (_conversionHigh.Period = (_conversionLow.Period = value));
            RecalculateValues();
        }
    }

    [LocalizedCategory(typeof(Resources), "Settings")]
    [DisplayName("Kijun-sen")]
    [Range(1, 10000)]
    public int Kijun
    {
        get
        {
            return _baseHigh.Period;
        }
        set
        {
            int num3 = (_baseHigh.Period = (_baseLow.Period = value));
            RecalculateValues();
        }
    }

    [LocalizedCategory(typeof(Resources), "Settings")]
    [DisplayName("Senkou Span B")]
    [Range(1, 10000)]
    public int Senkou
    {
        get
        {
            return _spanHigh.Period;
        }
        set
        {
            int num3 = (_spanHigh.Period = (_spanLow.Period = value));
            RecalculateValues();
        }
    }

    [LocalizedCategory(typeof(Resources), "Settings")]
    [DisplayName("Displacement")]
    [Range(1, 10000)]
    public int Displacement
    {
        get
        {
            return _displacement;
        }
        set
        {
            _displacement = value;
            RecalculateValues();
        }
    }
    [Display(
            Name = "Volume",
            Order = 30)]
    [Parameter]
    public decimal Volume { get; set; }

    [Display(ResourceType = typeof(Resources),
        Name = "ClosePositionOnStopping",
        Order = 40)]
    [Parameter]
    public bool ClosePositionOnStopping
    {
        get; set;


    }

    // Ichimoku Components


    private readonly Highest _baseHigh = new Highest
    {
        Period = 26
    };

    private readonly ValueDataSeries _baseLine = new ValueDataSeries("Base")
    {
        Color = Color.FromRgb((byte)153, (byte)21, (byte)21)
    };

    private readonly Lowest _baseLow = new Lowest
    {
        Period = 26
    };

    private readonly Highest _conversionHigh = new Highest
    {
        Period = 9
    };

    private readonly ValueDataSeries _conversionLine = new ValueDataSeries("Conversion")
    {
        Color = Color.FromRgb((byte)4, (byte)150, byte.MaxValue)
    };

    private readonly Lowest _conversionLow = new Lowest
    {
        Period = 9
    };

    private readonly RangeDataSeries _downSeries = new RangeDataSeries("Down")
    {
        RangeColor = Color.FromArgb((byte)100, byte.MaxValue, (byte)0, (byte)0)
    };

    private readonly ValueDataSeries _laggingSpan = new ValueDataSeries("Lagging Span")
    {
        Color = Color.FromRgb((byte)69, (byte)153, (byte)21)
    };

    private readonly ValueDataSeries _leadLine1 = new ValueDataSeries("Lead1")
    {
        Color = Colors.Green
    };

    private readonly ValueDataSeries _leadLine2 = new ValueDataSeries("Lead2")
    {
        Color = Colors.Red
    };

    private readonly Highest _spanHigh = new Highest
    {
        Period = 52
    };

    private readonly Lowest _spanLow = new Lowest
    {
        Period = 52
    };

    private readonly RangeDataSeries _upSeries = new RangeDataSeries("Up")
    {
        RangeColor = Color.FromArgb((byte)100, (byte)0, byte.MaxValue, (byte)0)
    };

    private int _days;

    private int _displacement = 26;

    private int _targetBar;

  
  

    public IchimokuKinkoHyoStrategy()
    {
        Name = "Ichimoku Kinko Hyo: Basic Strategy";

        // Initialize Ichimoku Components
        base.DenyToChangePanel = true;
        base.DataSeries[0] = _conversionLine;
        base.DataSeries.Add(_laggingSpan);
        base.DataSeries.Add(_baseLine);
        base.DataSeries.Add(_leadLine1);
        base.DataSeries.Add(_leadLine2);
        base.DataSeries.Add(_upSeries);
        base.DataSeries.Add(_downSeries);
        Volume = 1;

        if (CurrentPosition != 0)
        {
            _isInPosition = true;
        }
        else
        {
            _isInPosition = false;
        }

    }

    private decimal GetOrderVolume()
    {
        if (CurrentPosition == 0)
            return Volume;

        if (CurrentPosition > 0)
            return Volume + CurrentPosition;

        return Volume + Math.Abs(CurrentPosition);
    }


    private void OpenPosition(OrderDirections direction)
    {
        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = direction,
            Type = OrderTypes.Market,
            QuantityToFill = GetOrderVolume(),
        };

        OpenOrder(order);
    }

    private void CloseCurrentPosition()
    {
        var order = new Order
        {
            Portfolio = Portfolio,
            Security = Security,
            Direction = OrderDirections.Sell,
            Type = OrderTypes.Market,
            QuantityToFill = Math.Abs(CurrentPosition),
        };

        OpenOrder(order);
    }




    // Calculate Ichimoku Components
    protected override void OnCalculate(int bar, decimal value)
    {

        ClosePositionOnStopping = true;
        IndicatorCandle candle = GetCandle(bar);
        if (bar == 0)
        {
            base.DataSeries.ForEach(delegate (IDataSeries x)
            {
                x.Clear();
            });
            _targetBar = 0;
            if (_days > 0)
            {
                int num = 0;
                for (int num2 = base.CurrentBar - 1; num2 >= 0; num2--)
                {
                    _targetBar = num2;
                    if (IsNewSession(num2))
                    {
                        num++;
                        if (num == _days)
                        {
                            break;
                        }
                    }
                }

                if (_targetBar > 0)
                {
                    _conversionLine.SetPointOfEndLine(_targetBar - 1);
                    _laggingSpan.SetPointOfEndLine(_targetBar - _displacement);
                    _baseLine.SetPointOfEndLine(_targetBar - 1);
                    _leadLine1.SetPointOfEndLine(_targetBar + _displacement - 2);
                    _leadLine2.SetPointOfEndLine(_targetBar + _displacement - 2);
                }
            }
        }

        _conversionHigh.Calculate(bar, candle.High);
        _conversionLow.Calculate(bar, candle.Low);
        _baseHigh.Calculate(bar, candle.High);
        _baseLow.Calculate(bar, candle.Low);
        _spanHigh.Calculate(bar, candle.High);
        _spanLow.Calculate(bar, candle.Low);
        if (bar < _targetBar)
        {
            return;
        }

        _baseLine[bar] = (_baseHigh[bar] + _baseLow[bar]) / 2m;
        _conversionLine[bar] = (_conversionHigh[bar] + _conversionLow[bar]) / 2m;
        int index = bar + Displacement;
        _leadLine1[index] = (_conversionLine[bar] + _baseLine[bar]) / 2m;
        _leadLine2[index] = (_spanHigh[bar] + _spanLow[bar]) / 2m;
        if (bar - _displacement + 1 >= 0)
        {
            int num3 = bar - _displacement;
            _laggingSpan[num3] = candle.Close;
            if (bar == base.CurrentBar - 1)
            {
                for (int i = num3 + 1; i < base.CurrentBar; i++)
                {
                    _laggingSpan[i] = candle.Close;
                }
            }
        }

        if (_leadLine1[bar] == 0m || _leadLine2[bar] == 0m)
        {
            return;
        }

        if (_leadLine1[bar] > _leadLine2[bar])
        {
            _upSeries[bar].Upper = _leadLine1[bar];
            _upSeries[bar].Lower = _leadLine2[bar];
            if (_leadLine1[bar - 1] < _leadLine2[bar - 1])
            {
                _downSeries[bar] = _upSeries[bar];
            }
        }
        else
        {
            _downSeries[bar].Upper = _leadLine2[bar];
            _downSeries[bar].Lower = _leadLine1[bar];
            if (_leadLine1[bar - 1] > _leadLine2[bar - 1])
            {
                _upSeries[bar] = _downSeries[bar];
            }
        }
        // Entry/Exit Signals
        ////////////////////////////////////
       
        //bool tk_cross_bull = _conversionLine[1] > _baseLine[1]; //original way this was written
        //bool tk_cross_bear = _conversionLine[1] < _baseLine[1];
        bool tk_cross_bull = _conversionLine[bar] > _baseLine[bar]; //1st attempt to fix
        bool tk_cross_bear = _conversionLine[bar] < _baseLine[bar];
        //bool cs_cross_bull = Momentum - 1 > 0;
        //bool cs_cross_bear = Momentum - 1 < 0;
        //bool price_above_kumo = bar > _spanHigh[1];
        //bool price_below_kumo = bar < _spanLow[1];
        //bool lag_Above_Cloud = bar > _spanHigh[1];
        //bool lag_Below_Cloud = bar < _spanLow[1];

        bool bullish = tk_cross_bull /*&& cs_cross_bull*/ /*&& price_above_kumo*/ /*&& lag_Above_Cloud*/ && !IsInPosition && !bullish_Triggered;
        bool bearish = tk_cross_bear /*&& cs_cross_bear*/ /*&& price_below_kumo*/ /*&& lag_Below_Cloud*/ && IsInPosition && !bearish_Triggered;
        
        if (bullish && long_entry)
        {
            //cross up
            
            bullish_Triggered = true;
            bearish_Triggered = false;
            OpenPosition(OrderDirections.Buy);
            AddAlert("Alert1", "Bullish condition met. Place long trade.");


        }
    
        if (bearish && short_entry)
        {
            //cross down
            
            bullish_Triggered = false;
            bearish_Triggered = true;
            CloseCurrentPosition();
            AddAlert("Alert1", "Closed Position");



        }

        //if (bearish && !short_entry)
        //{
        //    //cross up
        //    OpenPosition(OrderDirections.Buy);
        //    AddAlert("Alert1", "Bullish condition met. Place long trade.");
        //}
        //if (bullish && !long_entry)
        //{
        //    //cross down
        //    OpenPosition(OrderDirections.Sell);
        //    AddAlert("Alert1", "Bearish condition met. Place short trade.");

        //}


        
    }
    protected override void OnStopping()
    {
        if (CurrentPosition != 0 && ClosePositionOnStopping)
        {
            RaiseShowNotification($"Closing current position {CurrentPosition} on stopping.", level: LoggingLevel.Warning);
            CloseCurrentPosition();
        }

        base.OnStopping();
    }
    
}