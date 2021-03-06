﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BasicallyMe.RobinhoodNet;

namespace RobinhoodDesktop.HomePage
{
    public partial class HomePageForm : Form
    {
        public HomePageForm()
        {
            InitializeComponent();
            this.FormClosing += HomePageForm_FormClosing;
            
            // Load the configuration
            this.Config = UserConfig.Load(UserConfig.CONFIG_FILE);

            // Create the interface to the stock data
            Robinhood = new RobinhoodInterface();
            DataAccessor.SetAccessor(new DataTableCache(Robinhood));
            Broker.SetBroker(Robinhood);

            if (String.IsNullOrEmpty(Config.DeviceToken))
            {
                Config.DeviceToken = Broker.Instance.GenerateDeviceToken();
                Config.Save(UserConfig.CONFIG_FILE);
            }

            UIList = new Panel();
            UIList.HorizontalScroll.Maximum = 0;
            UIList.AutoScroll = false;
            UIList.VerticalScroll.Enabled = false;
            UIList.Resize += (sender, e) =>
            {
                foreach(Control c in UIList.Controls)
                {
                    c.Size = new Size(UIList.Width - 10, c.Height);
                }
            };
            UIList.ControlAdded += UIList_Pack;
            UIList.ControlRemoved += UIList_Pack;
            UIList.Location = new Point(340, 20);
            this.Controls.Add(UIList);

            UiScrollBar = new CustomControls.CustomScrollbar();
            UiScrollBar.Minimum = 0;
            UiScrollBar.Maximum = UIList.Height;
            UiScrollBar.LargeChange = UiScrollBar.Maximum / UiScrollBar.Height;
            UiScrollBar.SmallChange = 15;
            UiScrollBar.Value = 0;
            UiScrollBar.Scroll += (sender, e) =>
            {
                //UIList.AutoScrollPosition = new Point(0, UiScrollBar.Value);
                UiScrollBar.Invalidate();
                //Application.DoEvents();
                UIList_Pack(sender, e);
            };
            UIList.Resize += (sender, e) =>
            {
                UiScrollBar.Bounds = new Rectangle(UIList.Right + 5, UIList.Top, 10, UIList.Height);
            };
            this.Controls.Add(UiScrollBar);

            // Create the account summary panel
            AccountSummaryPanel = new Panel();
            AccountSummaryPanel.Location = new Point(50, 20);
            AccountSummaryPanel.Size = new Size(270, 60);
            AccountSummaryPanel.Visible = true;
            Controls.Add(AccountSummaryPanel);
            AccountSummaryPanel.Controls.Add(SummaryTotalLabel = new Label());
            AccountSummaryPanel.Controls.Add(SummaryBuyingPowerLabel = new Label());
            int idx = 0;
            foreach(Label l in AccountSummaryPanel.Controls)
            {
                l.Size = new Size(AccountSummaryPanel.Width / AccountSummaryPanel.Controls.Count, AccountSummaryPanel.Height);
                l.Location = new Point(l.Width * idx++, 0);
                l.BackColor = GuiStyle.BACKGROUND_COLOR;
                l.ForeColor = GuiStyle.TEXT_COLOR;
                l.Font = new Font(GuiStyle.Font.Name, 12, FontStyle.Bold);
                l.TextAlign = ContentAlignment.TopCenter;
            }
            UiUpdateTick = new Timer();
            UiUpdateTick.Tick += (sender, e) =>
            {
                AccountSummaryPanel.Visible = AccountSummaryPanel.Bounds.Contains(PointToClient(MousePosition));
            };
            UiUpdateTick.Interval = 50;
            UiUpdateTick.Start();

            // Create the labels to show the account value and daily change
            CashLabel = new Label();
            CashLabel.Location = new Point(50, 20);
            CashLabel.Size = new Size(270, 40);
            CashLabel.BackColor = GuiStyle.BACKGROUND_COLOR;
            CashLabel.ForeColor = GuiStyle.TEXT_COLOR;
            CashLabel.Font = new Font(GuiStyle.Font.Name, 18, FontStyle.Bold);
            CashLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(CashLabel);
            ChangeLabel = new Label();
            ChangeLabel.Location = new Point(CashLabel.Location.X, CashLabel.Location.Y + CashLabel.Height);
            ChangeLabel.Size = new Size(CashLabel.Size.Width, 20);
            ChangeLabel.BackColor = GuiStyle.BACKGROUND_COLOR;
            ChangeLabel.ForeColor = GuiStyle.TEXT_COLOR;
            ChangeLabel.Font = new Font(GuiStyle.Font.Name, 8, FontStyle.Bold);
            ChangeLabel.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(ChangeLabel);


            // Create the buy/sell menu
            BuySell = new BuySellPanel();
            BuySell.Location = new Point(0, AccountSummaryPanel.Location.Y + AccountSummaryPanel.Height);
            BuySell.Size = new Size(AccountSummaryPanel.Location.X + AccountSummaryPanel.Width, this.Height - BuySell.Location.Y);
            BuySell.Visible = false;
            //BuySell.SubmitOrderButton.MouseClick += (sender, e) => {
                System.Threading.Tasks.Task orderMonitor = new Task((Action)(() =>
                {
                    List<Broker.Order> orders;
                    while((orders = Broker.Instance.GetOrders()).Count > 0)
                    {
                        // Check if the order gui matches the current state
                        List<StockList.StockLine> guiOrders = StockListHome.Stocks[StockList.ORDERS];
                        foreach(Broker.Order o in orders)
                        {
                            if(guiOrders.Find((a) => { return a.Symbol.Equals(o.Symbol); }) == null)
                            {
                                // Add the new order
                                StockListHome.Add(StockList.ORDERS, o.Symbol, new StockList.OrderSummary(o));
                            }
                        }

                        // Remove inactive orders from the GUI if needed
                        if(orders.Count < guiOrders.Count)
                        {
                            for(int i = 0; i < guiOrders.Count; i++)
                            {
                                if(orders.Find((a) => { return a.Symbol.Equals(guiOrders[i].Symbol); }) == null)
                                {
                                    // Remove the order
                                    guiOrders.RemoveAt(i--);
                                }
                            }
                        }

                        System.Threading.Thread.Sleep(250);
                    }
                }));
                orderMonitor.Start();
            //};
            Controls.Add(BuySell);


            // Create the search box
            SearchHome = new SearchList();
            SearchHome.Size = new Size(CashLabel.Width, 50);
            SearchHome.Location = new Point(CashLabel.Location.X, ChangeLabel.Location.Y + ChangeLabel.Height);
            SearchHome.AutoSize = true;
            SearchHome.AddToWatchlist += (string symbol) => { StockListHome.Add("Watchlist", symbol); };
            SearchHome.AddStockUi += CreateStockChart;
            Controls.Add(SearchHome);

            // Create the list of stocks being examined
            StockListHome = new StockList();
            StockListHome.Location = new Point(SearchHome.Location.X, SearchHome.Location.Y + 100);
            StockListHome.Size = new Size(300, 300);
            StockListHome.AddStockUi += CreateStockChart;
            StockListHome.GroupOrder = new List<string>()
            {
                StockList.NOTIFICATIONS,
                StockList.ORDERS,
                StockList.POSITIONS,
                StockList.WATCHLIST
            };
            Controls.Add(StockListHome);

            // Create the menu
            Menu = new MenuBar(Config);
            Menu.ToggleButton.Location = new Point(20, 20);
            Menu.LogIn.RememberLogIn.Checked = Config.RememberLogin;
            Menu.LogIn.LogInButton.MouseUp += (sender, e) => {
                HomePageForm_AccountUpdate();
                if(Broker.Instance.IsSignedIn())
                {
                    SaveConfig();
                }
            };
            Controls.Add(Menu.ToggleButton);

            // Set up the resize handler
            this.ResizeEnd += HomePageForm_ResizeEnd;
            HomePageForm_ResizeEnd(this, EventArgs.Empty);

            // Sign in if authentification is available
            if(Config.RememberLogin && !string.IsNullOrEmpty(Config.AuthenticationToken))
            {
                Broker.Instance.SignIn(Config.AuthenticationToken);
                
            }

            // Wait to request stock data until after the window has been created
            this.HandleCreated += (sender, e) =>
            {
                foreach(var configObj in Config.StockCharts)
                {
                    CreateStockChart(StockChartPanel.LoadConfig(configObj));
                }
                foreach(string symbol in Config.LocalWatchlist)
                {
                    StockListHome.Add(StockList.WATCHLIST, symbol);
                }
                HomePageForm_AccountUpdate();
            };
        }

        #region Variables
        public SearchList SearchHome;
        public StockList StockListHome;
        public RobinhoodInterface Robinhood;
        public UserConfig Config;
        public List<StockChartPanel> StockUIs = new List<StockChartPanel>();
        public Panel UIList;
        public MenuBar Menu;
        public Label CashLabel;
        public Label ChangeLabel;
        public Panel AccountSummaryPanel;
        public Label SummaryTotalLabel;
        public Label SummaryBuyingPowerLabel;
        private BuySellPanel BuySell;
        private CustomControls.CustomScrollbar UiScrollBar;
        private Timer UiUpdateTick;
        #endregion

        private void CreateStockChart(string symbol)
        {
            //if(!System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift))
            {
                // Check if a chart already exists for the stock
                foreach(var stock in StockUIs)
                {
                    if(symbol.Equals(stock.Symbol))
                    {
                        // Scroll to the stock
                        UiScrollBar.Value += stock.Location.Y;
                        return;
                    }
                }
            }

            // Add the stock to the chart list
            StockChartPanel ui = new StockChartPanel(symbol);
            CreateStockChart(ui);
            UIList_Pack(this, EventArgs.Empty);
            UiScrollBar.Value += ui.Location.Y;
        }

        private void CreateStockChart(StockChartPanel ui)
        {
            StockUIs.Add(ui);
            ui.Size = new Size(UIList.Width - 10, 250);
            ui.Resize += UIList_Pack;
            ui.CloseButton.MouseUp += (sender, e) => {
                StockUIs.Remove(ui);
                UIList.Controls.Remove(ui);
            };
            ui.BuyButton.MouseUp += (sender, e) => {
                BuySell.ShowOrderMenu(((StockChartPanel)ui).Symbol, Broker.Order.BuySellType.BUY);
            };
            ui.SellButton.MouseUp += (sender, e) => {
                BuySell.ShowOrderMenu(((StockChartPanel)ui).Symbol, Broker.Order.BuySellType.SELL);
            };
            ui.Chart.Updated += () => {
                float currentPrice = (float)ui.Chart.Source.Rows[ui.Chart.Source.Rows.Count - 1][StockChart.PRICE_DATA_TAG];
                float changePercent = (currentPrice / (float)ui.Chart.DailyData.Rows[ui.Chart.DailyData.Rows.Count - 1][StockChart.PRICE_DATA_TAG]) - 1.0f;
                ui.UpdateSummaryText(string.Format("{0} {1:c} ({2:P2})", ui.Symbol, currentPrice, changePercent));
            };
            UIList.Controls.Add(ui);
        }

        private void UIList_Pack(object sender, System.EventArgs e)
        {
            for(int times = 1; (UIList.Controls.Count > 0) && (times > 0); times--)
            {
                int y = -UiScrollBar.Value;
                foreach(Control c in UIList.Controls)
                {
                    c.Location = new Point(c.Location.X, y);
                    y += c.Height + 5;
                }
                UiScrollBar.Visible = ((UiScrollBar.Value + y) > UIList.Height);
                UiScrollBar.SmallChange = (UiScrollBar.Value + y) / UIList.Controls.Count;
                UiScrollBar.Maximum = (UiScrollBar.Value + y) - UIList.Height;

            }
            UiScrollBar.Invalidate();

        }

        private void HomePageForm_ResizeEnd(object sender, System.EventArgs e)
        {
            UIList.Size = new Size((this.Width - (StockListHome.Location.X + StockListHome.Width)) - 40, (this.Height - UIList.Location.Y) - 40);
            StockListHome.Size = new Size(StockListHome.Width, ((this.Height - StockListHome.Location.Y) - 40));
        }

        private void HomePageForm_AccountUpdate()
        {
            if(Broker.Instance.IsSignedIn())
            {
                Broker.Instance.GetAccountInfo((account) =>
                {
                    // Set the account buying power
                    BeginInvoke((Action)(() => {
                        CashLabel.Text = string.Format("{0:c}", account.TotalValue);
                        decimal open = 1;
                        decimal current = account.TotalValue;
                        decimal delta = current - open;
                        ChangeLabel.Text = string.Format("{0}{1:c} ({2:P2})", (delta >= 0) ? "+" : "-", delta, (delta / open));

                        SummaryTotalLabel.Text = string.Format("Total\n{0:c}", current);
                        SummaryBuyingPowerLabel.Text = string.Format("Buying Power\n{0:c}", account.BuyingPower);
                    }));

                    // Set the list of positions
                    Dictionary<string, decimal> positions = Broker.Instance.GetPositions();
                    StockListHome.Clear(StockList.POSITIONS);
                    foreach(KeyValuePair<string, decimal> pair in positions)
                    {
                        StockListHome.Add(StockList.POSITIONS, pair.Key, new StockList.PositionChangeSummary(DataAccessor.Subscribe(pair.Key, DataAccessor.SUBSCRIBE_FIVE_SEC)));
                    }
                });
            }
        }

        private void SaveConfig()
        {
            Config.LocalWatchlist.Clear();
            List<StockList.StockLine> watchlist;
            if(StockListHome.Stocks.TryGetValue(StockList.WATCHLIST, out watchlist))
            {
                foreach(var stock in watchlist)
                {
                    Config.LocalWatchlist.Add(stock.Symbol);
                }
            }
            Config.StockCharts.Clear();
            foreach(var chart in StockUIs)
            {
                Config.StockCharts.Add(chart.SaveConfig());
            }

            Config.RememberLogin = Menu.LogIn.RememberLogIn.Checked;
            if(Config.RememberLogin && Broker.Instance.IsSignedIn())
            {
                Config.AuthenticationToken = Broker.Instance.GetAuthenticationToken();
            }

            // Save the current user configuration
            Config.Save(UserConfig.CONFIG_FILE);
        }

        private void HomePageForm_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UiUpdateTick.Stop();
            while(UiUpdateTick.Enabled) ;
            Robinhood.Close();
            DataAccessor.Close();

            SaveConfig();
        }
    }
}
