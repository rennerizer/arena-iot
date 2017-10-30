using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using ArrayList = System.Collections.Generic.List<object>;

namespace PuzzleLogoDisplay
{
    public class PieceUpdatedEventArgs : EventArgs
    {
        public int PieceId { get; set; }
        public Point NewPosition { get; set; }
    }

    public class PiecesCorrectEventArgs : EventArgs
    {
        public bool[] PiecesCorrectArray { get; set; } = new bool[9];
    }

    public class GameOverEventArgs : EventArgs
    {
        public int TotalMoves { get; set; }
    }

    public class PuzzleState
    {
        public bool IsPlaying { get; set; }
        public int ColsAndRows { get; set; }
        public int[] Board { get; set; }
        public int TotalMoves { get; set; }
    }

    public class PuzzleGame
    {
        private int colsAndRows;
        private int[] board;
        private int totalMoves;
        private bool isPlaying;
        public TextBlock Congo;

        public int ColsAndRows { get { return this.colsAndRows; } }
        public int TotalMoves { get { return this.totalMoves; } }
        public bool IsPlaying { get { return this.isPlaying; } }
        public int[] BoardPieces { get { return this.board; } }

        public PuzzleGame(int colsAndRows, TextBlock Congo)
        {
            if (colsAndRows < 2)
            {
                throw new ArgumentOutOfRangeException("colsAndRows");
            }

            this.colsAndRows = colsAndRows;
            this.Congo = Congo;
            int totalPieces = colsAndRows * colsAndRows;
            this.board = new int[totalPieces];
            this.Reset();
        }

        public EventHandler GameStarted;
        public EventHandler<PieceUpdatedEventArgs> PieceUpdated;
        public EventHandler<PiecesCorrectEventArgs> PiecesCorrect;
        public EventHandler<GameOverEventArgs> GameOver;

        public void Reset()
        {
            this.isPlaying = false;
            int totalPieces = colsAndRows * colsAndRows;
            for (int n = 0; n < totalPieces - 1; n++)
            {
                this.board[n] = n;
                int nx = n / ColsAndRows;
                int ny = n % ColsAndRows;
                this.InvokePieceUpdated(n, new Point(nx, ny));
            }
        }

        public void NewGame()
        {
            int totalPieces = colsAndRows * colsAndRows;
            this.totalMoves = 0;

            // Initialize Board
            for (int n = 0; n < totalPieces - 1; n++)
            {
                this.board[n] = n;
            }

            Random rand = new Random(System.DateTime.Now.Second);
            for (int n = 0; n < 100; n++)
            {
                int n1 = rand.Next(totalPieces - 1);
                int n2 = rand.Next(totalPieces - 1);
                if (n1 != n2)
                {
                    int tmp = this.board[n1];
                    this.board[n1] = this.board[n2];
                    this.board[n2] = tmp;
                }
            }

            this.board[totalPieces - 1] = -1;

            for (int n = 0; n < totalPieces - 1; n++)
            {
                int nx = n / colsAndRows;
                int ny = n % colsAndRows;
                if (this.board[n] >= 0)
                {
                    this.InvokePieceUpdated(this.board[n], new Point(nx, ny));
                }
            }

            this.isPlaying = true;
            if (this.GameStarted != null)
            {
                this.GameStarted(this, null);
            }
        }

        // (0 = no movement)
        //     | 1 |
        //  ---+---+---
        //   4 | 0 | 2
        //  ---+---+---
        //     | 3 |
        public int CanMovePiece(int pieceId)
        {
            if (!this.isPlaying)
            {
                return 0;
            }

            int totalPieces = this.colsAndRows * this.colsAndRows;
            int boardLoc = -1;
            int emptyLoc = -1;

            for (int i = 0; i < totalPieces; i++)
            {
                if (this.board[i] == pieceId)
                {
                    boardLoc = i;
                }
                else if (this.board[i] == -1)
                {
                    emptyLoc = i;
                }
            }

            if ((boardLoc == emptyLoc + 1) ||
                (boardLoc == emptyLoc - 1) ||
                (boardLoc == emptyLoc + ColsAndRows) ||
                (boardLoc == emptyLoc - ColsAndRows))
            {
                if (boardLoc + 1 == emptyLoc)
                {
                    return 3;
                }
                else if (boardLoc - 1 == emptyLoc)
                {
                    return 1;
                }
                else if (boardLoc - this.ColsAndRows == emptyLoc)
                {
                    return 4;
                }
                else if (boardLoc + this.ColsAndRows == emptyLoc)
                {
                    return 2;
                }
            }

            return 0;
        }

        public bool MovePiece(int pieceId)
        {
            if (!this.isPlaying)
            {
                return false;
            }

            int totalPieces = this.colsAndRows * this.colsAndRows;
            int boardLoc = -1;
            int emptyLoc = -1;

            for (int i = 0; i < totalPieces; i++)
            {
                if (this.board[i] == pieceId)
                {
                    boardLoc = i;
                }
                else if (this.board[i] == -1)
                {
                    emptyLoc = i;
                }
            }

            // Check if we can move
            if ((boardLoc == emptyLoc + 1) ||
                (boardLoc == emptyLoc - 1) ||
                (boardLoc == emptyLoc + ColsAndRows) ||
                (boardLoc == emptyLoc - ColsAndRows))
            {
                int nx = emptyLoc / ColsAndRows;
                int ny = emptyLoc % ColsAndRows;

                this.board[emptyLoc] = pieceId;
                this.board[boardLoc] = -1;

                this.totalMoves++;
                this.InvokePieceUpdated(pieceId, new Point(nx, ny));

                this.CheckWinner(Congo);
                return true;
            }

            return false;
        }

        public void CheckWinner(TextBlock Congo)
        {
            bool completed = true;
            int totalPieces = colsAndRows * colsAndRows;
            bool[] correctPieces = new bool[totalPieces];
            for (int n = 0; n < totalPieces - 1; n++)
            {
                if (n != this.board[n])
                {
                    correctPieces[n] = false;
                    completed = false;
                    //break;
                }
                else
                    correctPieces[n] = true;
            }

            InvokePiecesCorrect(correctPieces);

            if (completed)
            {
                if (this.GameOver != null)
                {
                    Congo.Visibility = Visibility.Visible;
                    Congo.Opacity = 1;
                    this.GameOver(this, new GameOverEventArgs { TotalMoves = this.totalMoves });
                    //Scores newScore = new Scores
                    //{
                    //    Date = DateTime.Now,
                    //    NoOfMoves = this.totalMoves
                    //};
                    //App.ViewModel.AddScoresToDb(newScore);
                    //App.ViewModel.SaveChangesToDB();
                }
            }
        }

        public PuzzleState GetState()
        {
            return new PuzzleState
            {
                ColsAndRows = this.colsAndRows,
                IsPlaying = this.isPlaying,
                Board = this.board,
                TotalMoves = this.totalMoves
            };
        }

        public void SetState(PuzzleState state)
        {
            this.board = state.Board;
            this.totalMoves = state.TotalMoves;
            this.isPlaying = state.IsPlaying;
            this.colsAndRows = state.ColsAndRows;

            var totalPieces = this.colsAndRows * this.colsAndRows;
            for (int n = 0; n < totalPieces; n++)
            {
                int nx = n / colsAndRows;
                int ny = n % colsAndRows;
                if (this.board[n] >= 0)
                {
                    this.InvokePieceUpdated(this.board[n], new Point(nx, ny));
                }
            }

            this.CheckWinner(Congo);
            if (this.isPlaying && this.GameStarted != null)
            {
                this.GameStarted(this, null);
            }
        }

        private void InvokePieceUpdated(int pieceId, Point newPosition)
        {
            if (this.PieceUpdated != null)
            {
                this.PieceUpdated(this, new PieceUpdatedEventArgs
                {
                    PieceId = pieceId,
                    NewPosition = newPosition
                });
            }
        }

        private void InvokePiecesCorrect(bool[] piecesCorrectArray)
        {
            if (this.PiecesCorrect != null)
            {
                this.PiecesCorrect(this, new PiecesCorrectEventArgs
                {
                    PiecesCorrectArray = piecesCorrectArray
                });
            }
        }
    }
}
