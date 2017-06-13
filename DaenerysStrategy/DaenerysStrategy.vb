Imports System
Imports System.Collections.Generic
Imports TradingMotion.SDKv2.Algorithms
Imports TradingMotion.SDKv2.Algorithms.InputParameters
Imports TradingMotion.SDKv2.Markets.Charts
Imports TradingMotion.SDKv2.Markets.Orders
Imports TradingMotion.SDKv2.Markets.Indicators.Momentum
Imports TradingMotion.SDKv2.Markets.Indicators.StatisticFunctions

Namespace DaenerysStrategy

    ''' <summary>
    ''' Daenerys trading rules:
    '''   * Entry: Price breaks above RSI buy signal level (long entry) or below RSI sell signal level (short entry)
    '''   * Exit: Reversal RSI sell/buy signal or fixed Stop Loss order
    '''   * Filters: None
    ''' </summary>
    Public Class DaenerysStrategy
        Inherits Strategy

        Dim rsiIndicator As RSIIndicator
        Dim catastrophicStop As StopOrder

        Public Sub New(ByVal mainChart As Chart, ByVal secondaryCharts As List(Of Chart))
            MyBase.New(mainChart, secondaryCharts)
        End Sub

        ''' <summary>
        ''' Strategy Name
        ''' </summary>
        ''' <returns>The complete Name of the strategy</returns>
        Public Overrides ReadOnly Property Name As String
            Get
                Return "Daenerys Strategy"
            End Get
        End Property

        ''' <summary>
        ''' Security filter that ensures the Position will be closed at the end of the trading session.
        ''' </summary>
        Public Overrides ReadOnly Property ForceCloseIntradayPosition As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Security filter that sets a maximum open position size of 1 contract (either side)
        ''' </summary>
        Public Overrides ReadOnly Property MaxOpenPosition As UInteger
            Get
                Return 1
            End Get
        End Property

        ''' <summary>
        ''' This strategy uses the Advanced Order Management mode
        ''' </summary>
        Public Overrides ReadOnly Property UsesAdvancedOrderManagement As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Strategy Parameter definition
        ''' </summary>
        Public Overrides Function SetInputParameters() As InputParameterList

            Dim parameters As New InputParameterList()

            ' The previous N bars period RSI indicator will use
            parameters.Add(New InputParameter("RSI Period", 80))

            ' Break level of RSI indicator we consider a buy signal
            parameters.Add(New InputParameter("RSI Buy signal trigger level", 52))
            ' Break level of RSI indicator we consider a sell signal
            parameters.Add(New InputParameter("RSI Sell signal trigger level", 48))

            ' The distance between the entry and the fixed stop loss order
            parameters.Add(New InputParameter("Catastrophic Stop Loss ticks distance", 58))

            Return parameters

        End Function

        ''' <summary>
        ''' Initialization method
        ''' </summary>
        Public Overrides Sub OnInitialize()

            log.Debug("DaenerysStrategy onInitialize()")

            ' Adding an RSI indicator to strategy 
            ' (see http://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:relative_strength_index_rsi)
            rsiIndicator = New RSIIndicator(Bars.Close, Me.GetInputParameter("RSI Period"), Me.GetInputParameter("RSI Sell signal trigger level"), Me.GetInputParameter("RSI Buy signal trigger level"))
            Me.AddIndicator("RSI indicator", rsiIndicator)

        End Sub

        ''' <summary>
        ''' Strategy enter/exit/filtering rules
        ''' </summary>
        Public Overrides Sub OnNewBar()

            Dim stopMargin As Double = Me.GetInputParameter("Catastrophic Stop Loss ticks distance") * Me.GetMainChart().Symbol.TickSize

            Dim buySignal As Integer = Me.GetInputParameter("RSI Buy signal trigger level")
            Dim sellSignal As Integer = Me.GetInputParameter("RSI Sell signal trigger level")

            If rsiIndicator.GetRSI()(1) <= buySignal And rsiIndicator.GetRSI()(0) > buySignal And Me.GetOpenPosition() <> 1 Then

                If Me.GetOpenPosition() = 0 Then

                    ' BUY SIGNAL: Entering long and placing a catastrophic stop loss
                    Dim buyOrder As MarketOrder = New MarketOrder(OrderSide.Buy, 1, "Enter long position")
                    catastrophicStop = New StopOrder(OrderSide.Sell, 1, Me.Bars.Close(0) - stopMargin, "Catastrophic stop long exit")

                    Me.InsertOrder(buyOrder)
                    Me.InsertOrder(catastrophicStop)

                ElseIf Me.GetOpenPosition() = -1 Then

                    ' BUY SIGNAL: Closing short position and cancelling the catastrophic stop loss order
                    Dim exitShortOrder As MarketOrder = New MarketOrder(OrderSide.Buy, 1, "Exit short position (reversal exit signal)")

                    Me.InsertOrder(exitShortOrder)
                    Me.CancelOrder(catastrophicStop)

                End If

            ElseIf rsiIndicator.GetRSI()(1) >= sellSignal And rsiIndicator.GetRSI()(0) < sellSignal And Me.GetOpenPosition() <> -1 Then

                If Me.GetOpenPosition() = 0 Then

                    ' SELL SIGNAL: Entering short and placing a catastrophic stop loss
                    Dim sellOrder As MarketOrder = New MarketOrder(OrderSide.Sell, 1, "Enter short position")
                    catastrophicStop = New StopOrder(OrderSide.Buy, 1, Me.Bars.Close(0) + stopMargin, "Catastrophic stop short exit")

                    Me.InsertOrder(sellOrder)
                    Me.InsertOrder(catastrophicStop)

                ElseIf Me.GetOpenPosition() = -1 Then

                    ' SELL SIGNAL: Closing long position and cancelling the catastrophic stop loss order
                    Dim exitLongOrder As MarketOrder = New MarketOrder(OrderSide.Sell, 1, "Exit long position (reversal exit signal)")

                    Me.InsertOrder(exitLongOrder)
                    Me.CancelOrder(catastrophicStop)

                End If

            End If

        End Sub

    End Class
End Namespace
