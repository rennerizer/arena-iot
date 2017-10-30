using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Spi;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace PuzzleLogoDisplay
{
    public sealed partial class PuzzlePage : Page
    {
        private const double DoubleTapSpeed = 500;
        private const int ImageSize = 435;
        private PuzzleGame game;
        private Canvas[] puzzlePieces;
        private Stream imageStream;

        private long lastTapTicks;
        private int movingPieceId = -1;
        private int movingPieceDirection;
        private double movingPieceStartingPosition;

        bool[] piecesCorrectArray = new bool[9];
        bool[] fullLogoDisplay = new bool[9] { true, true, true, true, true, true, true, true, true };

        bool twinkleToggle = true;
        bool copToggle = true;

        List<bool> chaserArray = new List<bool>() { true, false, false, true, false, false };
        List<bool> copArray = new List<bool>() { true, false, false, false };
 
        public Stream ImageStream
        {
            get
            {
                return this.imageStream;
            }

            set
            {
                this.imageStream = value;
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(value.AsRandomAccessStream());
                this.PreviewImage.Source = bitmap;
                int i = 0;
                int pieceSize = ImageSize / this.game.ColsAndRows;
                for (int ix = 0; ix < this.game.ColsAndRows; ix++)
                {
                    for (int iy = 0; iy < this.game.ColsAndRows; iy++)
                    {
                        Image pieceImage = this.puzzlePieces[i].Children[0] as Image;
                        pieceImage.Source = bitmap;
                        i++;
                    }
                }
            }
        }

        public PuzzlePage()
        {
            this.InitializeComponent();

            // Puzzle Game
            this.game = new PuzzleGame(3, Congo);

            this.game.GameStarted += delegate
            {
                this.StatusPanel.Visibility = Visibility.Visible;
                //this.TapToContinueTextBlock.Opacity = 0;
                this.TotalMovesTextBlock.Text = this.game.TotalMoves.ToString();
            };

            this.game.GameOver += delegate
            {
                //this.TapToContinueTextBlock.Opacity = 1;
                this.Congo.Opacity = 1;
                this.CongratsBorder.Opacity = 1;
                //this.CongratsBorder.Visibility = Visibility.Visible;
                //this.Congo.Visibility = Visibility.Visible;
                this.StatusPanel.Visibility = Visibility.Visible;
                this.TotalMovesTextBlock.Text = this.game.TotalMoves.ToString();
            };

            this.game.PieceUpdated += delegate (object sender, PieceUpdatedEventArgs args)
            {
                int pieceSize = ImageSize / this.game.ColsAndRows;
                this.AnimatePiece(this.puzzlePieces[args.PieceId], Canvas.LeftProperty, "Canvas.Left", (int)args.NewPosition.X * pieceSize);
                this.AnimatePiece(this.puzzlePieces[args.PieceId], Canvas.TopProperty, "Canvas.Top", (int)args.NewPosition.Y * pieceSize);
                this.TotalMovesTextBlock.Text = this.game.TotalMoves.ToString();

                //if (this.game.TotalMoves > 2)
                //{
                //    this.ModeSwitch.Visibility = Visibility.Visible;
                //}
            };

            this.game.PiecesCorrect += delegate (object sender, PiecesCorrectEventArgs args)
            {
                // Send the array of correct pieces to the LED program, but only if there's a change
                if (!piecesCorrectArray.SequenceEqual<bool>(args.PiecesCorrectArray))
                {
                    piecesCorrectArray = args.PiecesCorrectArray;
                }
            };

            InitBoard();

            #region Initilize LED Display

            InitSpi();

            Unloaded += MainPage_Unloaded;

            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(50);
            dispatcherTimer.Tick += TimerTick;

            dispatcherTimer.Start();

            #endregion
        }

        private void ModeVegas_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTimerHandlers();

            dispatcherTimer.Tick += Vegas;
        }

        private void ModeChaser_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTimerHandlers();

            dispatcherTimer.Tick += Chaser;
        }

        private void ModeRevGen_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTimerHandlers();

            dispatcherTimer.Tick += RevGen;
        }

        private void ModePride_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTimerHandlers();

            dispatcherTimer.Tick += Pride;
        }

        private void ModeCops_Click(object sender, RoutedEventArgs e)
        {
            ClearAllTimerHandlers();

            dispatcherTimer.Tick += Cops;
        }

        //private void ModeSwitch_Click(object sender, RoutedEventArgs e)
        //{
        //    dispatcherTimer.Tick -= TimerTick;

        //    border.IsTapEnabled = false;

        //    //dispatcherTimer.Tick += Vegas;
        //    dispatcherTimer.Tick += Chaser;
        //}

        #region LED Setup

        private const string SPI_CONTROLLER_NAME = "SPI0";
  
        private const Int32 SPI_CHIP_SELECT_LINE = 0;

        private SpiDevice SpiLEDs;

        private int cycleCount = 0;

        private DispatcherTimer dispatcherTimer;

        private async Task InitSpi()
        {
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 20000; // 1MHz                              
                settings.Mode = SpiMode.Mode0;   // Clock Idle Low                                  

                string spiAqs = SpiDevice.GetDeviceSelector(SPI_CONTROLLER_NAME);
                var devicesInfo = await DeviceInformation.FindAllAsync(spiAqs);
                SpiLEDs = await SpiDevice.FromIdAsync(devicesInfo[0].Id, settings);
            }
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization Failed", ex);
            }
        }

        private void SendCommand(byte[] Command)
        {
            if (SpiLEDs != null)
                SpiLEDs.Write(Command);
        }

        private void TimerTick(object sender, object e)
        {
            byte[] data = new byte[300];

            // First 31 LEDs are Blue (93 bytes)
            for (int i = 0; i < 93; i += 3)
            {
                if (piecesCorrectArray[0] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue


                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 14 LEDs are White (42 bytes)
            for (int i = 93; i < 135; i += 3)
            {
                if (piecesCorrectArray[1] == true)
                {
                    data[i] = 0x37;     // Blue
                    data[i + 1] = 0x37; // Red
                    data[i + 2] = 0x37; // Green
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 6 LEDs are Blue (18 bytes) (r)
            for (int i = 135; i < 153; i += 3)
            {
                if (piecesCorrectArray[2] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 153; i < 183; i += 3)
            {
                if (piecesCorrectArray[3] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 7 LEDs are Blue (21 bytes) (v)
            for (int i = 183; i < 204; i += 3)
            {
                if (piecesCorrectArray[4] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 13 LEDs are Blue (39 bytes) (g)
            for (int i = 204; i < 243; i += 3)
            {
                if (piecesCorrectArray[5] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 243; i < 273; i += 3)
            {
                if (piecesCorrectArray[6] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 9 LEDs are Blue (27 bytes) (n)
            for (int i = 273; i < 300; i += 3)
            {
                if (piecesCorrectArray[7] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Send three times to ensure LEDs are correct
            SendCommand(data);
        }

        private void Vegas(object sender, object e)
        {
            byte[] data = new byte[300];

            for (int i = 1; i < 301; i += 3)
            {
                if (twinkleToggle == true && (i % 2 == 0))
                {
                    data[i - 1] = 0xFF; // Red
                    data[i] = 0xFF;     // Green
                    data[i + 1] = 0xFF; // Blue
                }
                else if (twinkleToggle == false && (i % 2 != 0))
                {
                    data[i - 1] = 0xFF; // Red
                    data[i] = 0xFF;     // Green
                    data[i + 1] = 0xFF; // Blue
                }
                else
                    data[i - 1] = data[i] = data[i + 1] = 0x00;
            }

            if (twinkleToggle) twinkleToggle = false; else twinkleToggle = true;

            SendCommand(data);
        }

        private void Chaser(object sender, object e)
        {
            byte[] data = new byte[300];

            // First 31 LEDs are Blue (93 bytes)
            for (int i = 0; i < 93; i += 3)
            {
                data[i] = 0x00;     // Red
                data[i + 1] = 0x00; // Green
                data[i + 2] = 0x80; // Blue
            }

            // Next 14 LEDs are White (42 bytes)
            for (int i = 93; i < 135; i += 3)
            {
                data[i] = 0x37;     // Blue
                data[i + 1] = 0x37; // Red
                data[i + 2] = 0x37; // Green
            }

            // Next 6 LEDs are Blue (18 bytes) (r)
            for (int i = 135; i < 153; i += 3)
            {
                if (chaserArray[0] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 153; i < 183; i += 3)
            {
                if (chaserArray[1] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 7 LEDs are Blue (21 bytes) (v)
            for (int i = 183; i < 204; i += 3)
            {
                if (chaserArray[2] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 13 LEDs are Blue (39 bytes) (g)
            for (int i = 204; i < 243; i += 3)
            {
                if (chaserArray[3] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 243; i < 273; i += 3)
            {
                if (chaserArray[4] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 9 LEDs are Blue (27 bytes) (n)
            for (int i = 273; i < 300; i += 3)
            {
                if (chaserArray[5] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            SendCommand(data);

            // Single Chaser

            int index = chaserArray.FindIndex(c => c == true);          // Find the index that was on

            if (index == 5) index = -1;                                 // Account for the end of the list

            chaserArray = chaserArray.Select(x => false).ToList();      // Set everything to off

            chaserArray[index + 1] = true;                              // Turn the next one on

            // Double Chaser
            //List<bool> newChaserArray = new List<bool>(6) { false, false, false, false, false, false };

            //newChaserArray[0] = chaserArray[5] ? true : false;
            //newChaserArray[1] = chaserArray[0] ? true : false;
            //newChaserArray[2] = chaserArray[1] ? true : false;
            //newChaserArray[3] = chaserArray[2] ? true : false;
            //newChaserArray[4] = chaserArray[3] ? true : false;
            //newChaserArray[5] = chaserArray[4] ? true : false;

            //chaserArray = newChaserArray;
        }

        private void RevGen(object sender, object e)
        {
            byte[] data = new byte[300];

            // First 31 LEDs are Blue (93 bytes)
            for (int i = 0; i < 93; i += 3)
            {
                if (fullLogoDisplay[0] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 14 LEDs are White (42 bytes)
            for (int i = 93; i < 135; i += 3)
            {
                if (fullLogoDisplay[1] == true)
                {
                    data[i] = 0x37;     // Blue
                    data[i + 1] = 0x37; // Red
                    data[i + 2] = 0x37; // Green
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 6 LEDs are Blue (18 bytes) (r)
            for (int i = 135; i < 153; i += 3)
            {
                if (fullLogoDisplay[2] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 153; i < 183; i += 3)
            {
                if (fullLogoDisplay[3] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 7 LEDs are Blue (21 bytes) (v)
            for (int i = 183; i < 204; i += 3)
            {
                if (fullLogoDisplay[4] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 13 LEDs are Blue (39 bytes) (g)
            for (int i = 204; i < 243; i += 3)
            {
                if (fullLogoDisplay[5] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 243; i < 273; i += 3)
            {
                if (fullLogoDisplay[6] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 9 LEDs are Blue (27 bytes) (n)
            for (int i = 273; i < 300; i += 3)
            {
                if (fullLogoDisplay[7] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            SendCommand(data);

            // Since this is a static display, let's ensure the LED's aren't refreshed
            ClearAllTimerHandlers();
        }

        private void Pride(object sender, object e)
        {
            byte[] data = new byte[300];

            // First 31 LEDs are Blue (93 bytes)
            for (int i = 0; i < 93; i += 3)
            {
                if (fullLogoDisplay[0] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 14 LEDs are White (42 bytes)
            for (int i = 93; i < 135; i += 3)
            {
                if (fullLogoDisplay[1] == true)
                {
                    data[i] = 0x37;     // Blue
                    data[i + 1] = 0x37; // Red
                    data[i + 2] = 0x37; // Green
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 6 LEDs are Blue (18 bytes) (r)
            for (int i = 135; i < 153; i += 3)
            {
                if (fullLogoDisplay[2] == true)
                {
                    data[i] = 0xD4;     // Red
                    data[i + 1] = 0x06; // Green
                    data[i + 2] = 0x06; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 153; i < 183; i += 3)
            {
                if (fullLogoDisplay[3] == true)
                {
                    data[i] = 0xEE;     // Red
                    data[i + 1] = 0x9C; // Green
                    data[i + 2] = 0x00; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 7 LEDs are Blue (21 bytes) (v)
            for (int i = 183; i < 204; i += 3)
            {
                if (fullLogoDisplay[4] == true)
                {
                    data[i] = 0xE3;     // Red
                    data[i + 1] = 0xFF; // Green
                    data[i + 2] = 0x00; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 13 LEDs are Blue (39 bytes) (g)
            for (int i = 204; i < 243; i += 3)
            {
                if (fullLogoDisplay[5] == true)
                {
                    data[i] = 0x06;     // Red
                    data[i + 1] = 0xBF; // Green
                    data[i + 2] = 0x00; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 243; i < 273; i += 3)
            {
                if (fullLogoDisplay[6] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x1A; // Green
                    data[i + 2] = 0x98; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 9 LEDs are Blue (27 bytes) (n)
            for (int i = 273; i < 300; i += 3)
            {
                if (fullLogoDisplay[7] == true)
                {
                    data[i] = 0x4B;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x82; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            SendCommand(data);

            // Since this is a static display, let's ensure the LED's aren't refreshed
            ClearAllTimerHandlers();
        }

        private void Cops(object sender, object e)
        {
            byte[] data = new byte[300];

            for (int i = 1; i < 135; i += 3)
            {
                if (copToggle == true && (i < 93))
                {
                    data[i] = 0xD4;     // Red
                    data[i + 1] = 0x06; // Green
                    data[i + 2] = 0x06; // Blue
                }
                else if (copToggle == false && (i >= 93 && i < 135))
                {
                    data[i] = 0xD4;     // Red
                    data[i + 1] = 0x06; // Green
                    data[i + 2] = 0x06; // Blue
                }
                else
                    data[i - 1] = data[i] = data[i + 1] = 0x00;
            }

            for (int i = 243; i < 300; i += 3)
            {
                if (copToggle == true && (i >= 243 && i < 273))
                {
                    data[i] = 0xD4;     // Red
                    data[i + 1] = 0x06; // Green
                    data[i + 2] = 0x06; // Blue
                }
                else if (copToggle == false && (i >= 273 && i < 300))
                {
                    data[i] = 0xD4;     // Red
                    data[i + 1] = 0x06; // Green
                    data[i + 2] = 0x06; // Blue
                }
                else
                    data[i - 1] = data[i] = data[i + 1] = 0x00;
            }

            // Next 6 LEDs are Blue (18 bytes) (r)
            for (int i = 135; i < 153; i += 3)
            {
                if (copArray[0] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 10 LEDs are Blue (30 bytes) (e)
            for (int i = 153; i < 183; i += 3)
            {
                if (copArray[1] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 7 LEDs are Blue (21 bytes) (v)
            for (int i = 183; i < 204; i += 3)
            {
                if (copArray[2] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            // Next 13 LEDs are Blue (39 bytes) (g)
            for (int i = 204; i < 243; i += 3)
            {
                if (copArray[3] == true)
                {
                    data[i] = 0x00;     // Red
                    data[i + 1] = 0x00; // Green
                    data[i + 2] = 0x80; // Blue
                }
                else
                    data[i] = data[i + 1] = data[i + 2] = 0x00;
            }

            SendCommand(data);

            if (copToggle) copToggle = false; else copToggle = true;

            int index = copArray.FindIndex(c => c == true);          // Find the index that was on

            if (index == 3) index = -1;                                 // Account for the end of the list

            copArray = copArray.Select(x => false).ToList();      // Set everything to off

            copArray[index + 1] = true;                              // Turn the next one on
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            /* Cleanup */
            SpiLEDs.Dispose();
        }

        #endregion

        private void InitBoard()
        {
            int totalPieces = this.game.ColsAndRows * this.game.ColsAndRows;
            int pieceSize = ImageSize / this.game.ColsAndRows;
            this.puzzlePieces = new Canvas[totalPieces];
            int nx = 0;
            for (int ix = 0; ix < this.game.ColsAndRows; ix++)
            {
                for (int iy = 0; iy < this.game.ColsAndRows; iy++)
                {
                    nx = (ix * this.game.ColsAndRows) + iy;
                    Image image = new Image();
                    image.SetValue(FrameworkElement.NameProperty, "PuzzleImage_" + nx);
                    image.Height = ImageSize;
                    image.Width = ImageSize;
                    image.Stretch = Stretch.UniformToFill;
                    //image.ManipulationMode = ManipulationModes.All;
                    //image.ManipulationStarted += Page_ManipulationStarted;
                    //image.ManipulationDelta += Page_ManipulationDelta;
                    //image.ManipulationCompleted += Page_ManipulationCompleted;
                    RectangleGeometry r = new RectangleGeometry();
                    r.Rect = new Rect((ix * pieceSize), (iy * pieceSize), pieceSize, pieceSize);
                    image.Clip = r;
                    image.SetValue(Canvas.TopProperty, Convert.ToDouble(iy * pieceSize * -1));
                    image.SetValue(Canvas.LeftProperty, Convert.ToDouble(ix * pieceSize * -1));

                    this.puzzlePieces[nx] = new Canvas();
                    this.puzzlePieces[nx].SetValue(FrameworkElement.NameProperty, "PuzzlePiece_" + nx);
                    this.puzzlePieces[nx].Width = pieceSize;
                    this.puzzlePieces[nx].Height = pieceSize;
                    this.puzzlePieces[nx].Children.Add(image);
                    this.puzzlePieces[nx].PointerPressed += PuzzlePage_PointerPressed;
                    if (nx < totalPieces - 1)
                    {
                        this.GameContainer.Children.Add(this.puzzlePieces[nx]);
                    }
                }
            }

            // Retrieve image
            Uri uri = new Uri("ms-appx:///Assets/great-divide-02.png", UriKind.Absolute);

            Stream imageStream = GetImageStream(uri).Result;

            this.ImageStream = imageStream;

            this.game.Reset();
        }

        private async Task<Stream> GetImageStream(Uri uri)
        {
            var fileToRead = await StorageFile.GetFileFromApplicationUriAsync(uri);

            return await fileToRead.OpenStreamForReadAsync();
        }

        private void TapToContinueTextBlock_Click(object sender, RoutedEventArgs e)
        {
            this.game.NewGame();

            this.piecesCorrectArray = new bool[] { false, false, false, false, false, false, false, false, false };

            this.Congo.Opacity = 0;
            this.CongratsBorder.Opacity = 0;

            ClearAllTimerHandlers();
            
            this.dispatcherTimer.Tick += TimerTick;
        }

        private void ClearAllTimerHandlers()
        {
            this.dispatcherTimer.Tick -= TimerTick;
            this.dispatcherTimer.Tick -= Vegas;
            this.dispatcherTimer.Tick -= Chaser;
            this.dispatcherTimer.Tick -= RevGen;
            this.dispatcherTimer.Tick -= Pride;
            this.dispatcherTimer.Tick -= Cops;
        }

        private void PuzzlePage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Image image = e.OriginalSource as Image;

            if (image != null)
            {
                int pieceIx = Convert.ToInt32(image.Name.ToString().Substring(12));
                Canvas piece = this.FindName("PuzzlePiece_" + pieceIx) as Canvas;
                if (piece != null)
                {
                    int totalPieces = this.game.ColsAndRows * this.game.ColsAndRows;
                    for (int i = 0; i < totalPieces; i++)
                    {
                        if (piece == this.puzzlePieces[i] && this.game.CanMovePiece(i) > 0)
                        {
                            int direction = this.game.CanMovePiece(i);
                            DependencyProperty axisProperty = (direction % 2 == 0) ? Canvas.LeftProperty : Canvas.TopProperty;
                            this.movingPieceDirection = direction;
                            this.movingPieceStartingPosition = Convert.ToDouble(piece.GetValue(axisProperty));
                            this.movingPieceId = i;

                            this.game.MovePiece(this.movingPieceId);

                            break;
                        }
                    }
                }
            }
        }

        private void AnimatePiece(DependencyObject piece, DependencyProperty dp, string propertyPath, double newValue)
        {
            Storyboard storyBoard = new Storyboard();
            Storyboard.SetTarget(storyBoard, piece);
            Storyboard.SetTargetProperty(storyBoard, propertyPath);
            storyBoard.Children.Add(new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                From = Convert.ToInt32(piece.GetValue(dp)),
                To = Convert.ToDouble(newValue),
                EasingFunction = new SineEase()
            });
            storyBoard.Begin();
        }

        //private void Page_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        //{
        //    if (this.game.IsPlaying && e.Container is Image && e.Container.GetValue(FrameworkElement.NameProperty).ToString().StartsWith("PuzzleImage_"))
        //    {
        //        int pieceIx = Convert.ToInt32(e.Container.GetValue(FrameworkElement.NameProperty).ToString().Substring(12));
        //        Canvas piece = this.FindName("PuzzlePiece_" + pieceIx) as Canvas;
        //        if (piece != null)
        //        {
        //            int totalPieces = this.game.ColsAndRows * this.game.ColsAndRows;
        //            for (int i = 0; i < totalPieces; i++)
        //            {
        //                if (piece == this.puzzlePieces[i] && this.game.CanMovePiece(i) > 0)
        //                {
        //                    int direction = this.game.CanMovePiece(i);
        //                    DependencyProperty axisProperty = (direction % 2 == 0) ? Canvas.LeftProperty : Canvas.TopProperty;
        //                    this.movingPieceDirection = direction;
        //                    this.movingPieceStartingPosition = Convert.ToDouble(piece.GetValue(axisProperty));
        //                    this.movingPieceId = i;
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //}

        //private void Page_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        //{
        //    if (this.movingPieceId > -1)
        //    {
        //        int pieceSize = ImageSize / this.game.ColsAndRows;
        //        Canvas movingPiece = this.puzzlePieces[this.movingPieceId];

        //        // validate direction
        //        DependencyProperty axisProperty;
        //        double normalizedValue;

        //        if (this.movingPieceDirection % 2 == 0)
        //        {
        //            axisProperty = Canvas.LeftProperty;
        //            normalizedValue = e.Cumulative.Translation.X;
        //        }
        //        else
        //        {
        //            axisProperty = Canvas.TopProperty;
        //            normalizedValue = e.Cumulative.Translation.Y;
        //        }

        //        // enforce drag constraints
        //        // (top or left)
        //        if (this.movingPieceDirection == 1 || this.movingPieceDirection == 4)
        //        {
        //            if (normalizedValue < -pieceSize)
        //            {
        //                normalizedValue = -pieceSize;
        //            }
        //            else if (normalizedValue > 0)
        //            {
        //                normalizedValue = 0;
        //            }
        //        }
        //        // (bottom or right)
        //        else if (this.movingPieceDirection == 3 || this.movingPieceDirection == 2)
        //        {
        //            if (normalizedValue > pieceSize)
        //            {
        //                normalizedValue = pieceSize;
        //            }
        //            else if (normalizedValue < 0)
        //            {
        //                normalizedValue = 0;
        //            }
        //        }

        //        // set position
        //        movingPiece.SetValue(axisProperty, normalizedValue + this.movingPieceStartingPosition);
        //    }
        //}

        //private void Page_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        //{
        //    if (this.movingPieceId > -1)
        //    {
        //        int pieceSize = ImageSize / this.game.ColsAndRows;
        //        Canvas piece = this.puzzlePieces[this.movingPieceId];

        //        // check for double tapping
        //        if (TimeSpan.FromTicks(DateTime.Now.Ticks - this.lastTapTicks).TotalMilliseconds < DoubleTapSpeed)
        //        {
        //            // force move
        //            this.game.MovePiece(this.movingPieceId);
        //            this.lastTapTicks = int.MinValue;
        //        }
        //        else
        //        {
        //            // calculate moved distance
        //            DependencyProperty axisProperty = (this.movingPieceDirection % 2 == 0) ? Canvas.LeftProperty : Canvas.TopProperty;
        //            string stringProperty = (this.movingPieceDirection % 2 == 0) ? "Canvas.Left" : "Canvas.Top";

        //            double minRequiredDisplacement = pieceSize / 3;
        //            double diff = Math.Abs(Convert.ToDouble(piece.GetValue(axisProperty)) - this.movingPieceStartingPosition);

        //            // did it get halfway across?
        //            if (diff > minRequiredDisplacement)
        //            {
        //                // move piece
        //                this.game.MovePiece(this.movingPieceId);
        //            }
        //            else
        //            {
        //                // restore piece
        //                this.AnimatePiece(piece, axisProperty, stringProperty, this.movingPieceStartingPosition);
        //            }
        //        }

        //        this.movingPieceId = -1;
        //        this.movingPieceStartingPosition = 0;
        //        this.movingPieceDirection = 0;
        //        this.lastTapTicks = DateTime.Now.Ticks;
        //    }
        //}
    }
}
