using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PanTiltServoMVVM;
using Pololu.Usc;
using Pololu.UsbWrapper;
using System.Windows.Threading;

namespace ViPIR_MVVM_App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        PanTiltServoVM _model = new PanTiltServoVM();
        DispatcherTimer _dispatcherTimer = new DispatcherTimer();


        public MainWindow()
        {
            InitializeComponent();

            DataContext = _model;

            _model.ConnectModule();

            _dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            _dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50); // 100ms is max resolution from USB polling

            StartPolling();
        }


        private void StartPolling()
        {
            _model.StartPollingUsb();
            _dispatcherTimer.Start();
        }


        private void StopPolling()
        {
            _model.StopPollingUsb();
            _dispatcherTimer.Stop();
        }


        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // get the coordinates of the click
            int xPos = (int)Math.Round(100 * e.GetPosition(ClickGridCtrl).X / ClickGridCtrl.Width);
            int yPos = (int)Math.Round(100 * e.GetPosition(ClickGridCtrl).Y / ClickGridCtrl.Height);

            try
            {
                _model.PanPercentGoal = xPos;
                _model.TiltPercentGoal = yPos;
                MoveReticle(e.GetPosition(ClickGridCtrl));
            }
            catch { }
        }


        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            int xPos = (int)Math.Round(100 * e.GetPosition(ClickGridCtrl).X / ClickGridCtrl.Width);
            int yPos = (int)Math.Round(100 * e.GetPosition(ClickGridCtrl).Y / ClickGridCtrl.Height);

            if (e.LeftButton == MouseButtonState.Pressed &&
                xPos >= 0 &&
                xPos <= 100 &&
                yPos >= 0 &&
                yPos <= 100)
            {             
                try
                {
                    _model.PanPercentGoal = xPos;
                    _model.TiltPercentGoal = yPos;
                    MoveReticle(e.GetPosition(ClickGridCtrl));
                }
                catch { }
            }
        }


        private void MoveReticle(Point point)
        {
            PosCircleCtrl.Visibility = Visibility.Visible;
            Canvas.SetTop(PosCircleCtrl, point.Y - PosCircleCtrl.Height / 2);
            Canvas.SetLeft(PosCircleCtrl, point.X - PosCircleCtrl.Width / 2);
        }


        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (_model.IsPolling)
            {
                var topOffset = ((double)_model.TiltPercentActual / 100.0) * ClickGridCtrl.Height;
                var leftOffset = ((double)_model.PanPercentActual / 100.0) * ClickGridCtrl.Width;

                ActualCircleCtrl.Visibility = Visibility.Visible;
                Canvas.SetTop(ActualCircleCtrl,topOffset - ActualCircleCtrl.Height / 2);
                Canvas.SetLeft(ActualCircleCtrl,leftOffset - ActualCircleCtrl.Width / 2);
            }
            else
            {
                ActualCircleCtrl.Visibility = Visibility.Hidden;
            }
        }
    }
}
