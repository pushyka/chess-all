﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

using chess.Model;

namespace chess.Util
{
    public class Evaluator : IEvaluator
    {
        const int UNIQUE_PIECE_NUM = 12;
        const int SIZE = 8;
        // the showdialog for promotion selection popup requires a reference to the main display form
        // if i want that main form to be disabled while the user selects a promotion piece
        private System.Windows.Forms.Form viewRef = null;

        public List<List<Tuple<int, int>>>[][,] rayArray;
        public List<List<Tuple<int, int>>>[][,] rayArrayPawnCapture;

        /* Takes an input string from a view / source and returns true if it is
        in the correct chess board move format e.g. 'A5 A6'. The corresponding
        move object is also created and passed to the non-local move variable. */
        public bool ValidateInput(string input, ref FormedMove move)
        {
            // these lists contain only the valid file and ranks
            // and are indexed such that a file/rank will correspond to the col/row pos
            List<char> validFiles = "ABCDEFGH".ToList();
            List<int> validRanks = new List<int>(new int[] { 8, 7, 6, 5, 4, 3, 2, 1 });
            bool validMove = false;
            
            string[] chessBoardLocs = input.Split();
            Tuple<int, int> chessBoardPos1;
            Tuple<int, int> chessBoardPos2;

            if (chessBoardLocs.Length == 2)
            {
                string chessBoardLoc1 = chessBoardLocs[0];
                string chessBoardLoc2 = chessBoardLocs[1];
                {
                    char loc1File = Char.ToUpper(chessBoardLoc1[0]);
                    int loc1Rank = (int)Char.GetNumericValue(chessBoardLoc1[1]);

                    char loc2File = Char.ToUpper(chessBoardLoc2[0]);
                    int loc2Rank = (int)Char.GetNumericValue(chessBoardLoc2[1]);

                    if (validFiles.Contains(loc1File) && validRanks.Contains(loc1Rank) &&
                       (validFiles.Contains(loc2File) && validRanks.Contains(loc2Rank)))
                    {
                        int loc1Col = validFiles.IndexOf(loc1File);
                        int loc1Row = validRanks.IndexOf(loc1Rank);

                        int loc2Col = validFiles.IndexOf(loc2File);
                        int loc2Row = validRanks.IndexOf(loc2Rank);

                        chessBoardPos1 = Tuple.Create<int, int>(loc1Row, loc1Col);
                        chessBoardPos2 = Tuple.Create<int, int>(loc2Row, loc2Col);
                        move = new FormedMove(chessBoardPos1, chessBoardPos2);
                        validMove = true;
                    }
                }
            }
            return validMove;
        }


        /* Take a valid-format move object and check if it may be legally 
        applied to the current chess game. It must pass the following checks:
        -the locations are distinct and
        -the A tile contains a piece of the current player
        Also one of the following:
            -the B tile contains a piece of the current player (further castle checks) or
            -the B tile contains a piece of the opponent player (further capture checks) or
            -the B tile is empty:
                -(further en passant checks)
                -(further movement checks)
        Finally it must check, if the move were to be applied, that it does not leave the 
        current player's king in check.*/
        public bool IsValidMove(ref FormedMove move, ChessPositionModel cpm, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool validMove = false;
            Tile tileA = new Tile();
            Tile tileB = new Tile();

            if (IsMovePositionsDistinct(move))
            {
                if (IsMoveAPieceCurPlayer(move, cpm, ref tileA))
                {
                    // check if its a castling
                    if (IsMoveBPieceCurPlayer(move, cpm, ref tileB))
                    {
                        validMove = false; // IsLegalCastle? - this involves calls to isKingInCheck
                        move.MoveType = EChessMoveTypes.Castle;
                    }
                    // check if its a capture
                    else if (IsMoveBPieceOtherPlayer(move, cpm, ref tileB))
                    {
                        validMove = IsCaptureLegal(move, cpm);
                        move.MoveType = EChessMoveTypes.Capture;
                    }
                    // check if its a movement
                    else if (IsMoveBEmpty(move, cpm, ref tileB))
                    {
                        if (validMove = IsPieceMovementLegal(move, cpm))
                        {
                            move.MoveType = EChessMoveTypes.Movement;
                        }
                        else if (validMove = IsEnPassantCaptureLegal(move, cpm))
                        {
                            move.MoveType = EChessMoveTypes.EpMovement;
                        }
                    }
                }
            }
            // if its a valid move so far, check if there is a pawn promotion,
            // then apply the move to a copy of the chess position and
            // finally check it passes a check test
            if (validMove)
            {
                // if there is a pawn promotion the player is prompted for the promotion piece and it is added to the move object
                MoveIncludesPawnPromotion(ref move, cpm);

                // a copy of the chess position is made so that the move may
                // be applied in order for the king-check to be checked without
                // causing the chess position to update the display
                ChessPosition cpCopy = cpm.getChessPositionCopy();
                cpCopy.applyMove(move);
                // after this application of the move
                if (IsKingInCheck(cpCopy, ref kingCheckedBy))
                    validMove = false;
            }
            return validMove;
        }


        /* Take a move object and return true if the A and B
        positions are distinct from each other. */
        private bool IsMovePositionsDistinct(FormedMove move)
        {
            Tuple<int, int> posA = move.PosA;
            Tuple<int, int> posB = move.PosB;
            return !posB.Equals(posA);
        }


        /* Takes a move object and a chess board object and returns true if
        the piece on the A position of the move belongs to the current player. */
        private bool IsMoveAPieceCurPlayer(FormedMove move, ChessPosition cpm, ref Tile tileA)
        {
            bool result = false;
            tileA = cpm.Board[move.PosA.Item1, move.PosA.Item2];
            if (!tileA.IsEmpty())
                if (cpm.Player.Owns(tileA.piece))
                    result = true;
            return result;
        }


        /* Takes a move object and a chess board object and returns true if
        the piece on the B position of the move belongs to the current player. 

        TODO: merge into previous*/
        private bool IsMoveBPieceCurPlayer(FormedMove move, ChessPosition cpm, ref Tile tileB)
        {
            bool result = false;
            tileB = cpm.Board[move.PosB.Item1, move.PosB.Item2];
            if (!tileB.IsEmpty())
                if (cpm.Player.Owns(tileB.piece))
                    result = true;
            return result;
        }


        /* Takes a move object and a chess board object and returns true if
        the piece on the B position of the move belongs to the other player.
        
        TODO: replace with alternate call to previous functions*/
        private bool IsMoveBPieceOtherPlayer(FormedMove move, ChessPosition cpm, ref Tile tileB)
        {
            bool result = false;
            tileB = cpm.Board[move.PosB.Item1, move.PosB.Item2];
            if (!tileB.IsEmpty())
                if (!cpm.Player.Owns(tileB.piece))
                    result = true;
            return result;
        }


        /* Takes a move object and a chess board object and returns true if
        the tile on the B position of the move is empty. */
        private bool IsMoveBEmpty(FormedMove move, ChessPosition cpm, ref Tile tileB)
        {
            tileB = cpm.Board[move.PosB.Item1, move.PosB.Item2];
            return tileB.IsEmpty();
        }


        /* Takes a move which so far meets the requirements for a Movement type
        move on the chess board. This function checks that one of the moving piece's 
        rays covers this move object (The piece itself can move in this direction/way)
        and that the way is not blocked by another piece. */
        private bool IsPieceMovementLegal(FormedMove move, ChessPosition cpm)
        {
            bool isLegalMovement = true;
            Tuple<int, int> posA = move.PosA;
            Tuple<int, int> posB = move.PosB;
            Tile tileA = cpm.Board[posA.Item1, posA.Item2];
            List<List<Tuple<int, int>>> movementRays;
            // get the rays for a piece of type=piece.Val and location=posA
           
            movementRays = GetPieceRay(tileA.piece.Val, posA);
            ;
            List<Tuple<int, int>> moveRayUsed = null;
            foreach (List<Tuple<int, int>> ray in movementRays)
            {
                // for pawns the rays are comprise TWO forward positions (from first move)
                // so if the pawn has already .MovedOnce don not consider the second position in the ray
                // as the pawn can no longer legally move to that position
                if ((tileA.piece.Val == EGamePieces.WhitePawn || tileA.piece.Val == EGamePieces.BlackPawn) &&
                    (tileA.piece.MovedOnce))
                {
                    ;
                    if (ray[0].Equals(posB))
                    {
                        ;
                        moveRayUsed = ray;
                        break;
                    }
                }
                // for all other cases consider the entirety of the ray
                else
                {
                    if (ray.Contains(posB))
                    {
                        moveRayUsed = ray;
                        break;
                    }
                }


            }
            if (moveRayUsed != null)
                // check there are no intermediate Pieces blocking the movement
                foreach (Tuple<int, int> tilePos in moveRayUsed)
                {
                    Tile tileAtPosition = cpm.Board[tilePos.Item1, tilePos.Item2];
                    if (!tileAtPosition.IsEmpty())
                    {
                        isLegalMovement = false;
                        break;
                    }
                    if (tilePos.Equals(posB))
                        break;
                }
            else
                isLegalMovement = false;
        
            return isLegalMovement;
        }


        /* Takes a move which so far meets the requirements for an EnPassant type
        move on the chess board. This function checks that one of the moving pawn's 
        diagonal rays covers this move object (The piece itself can move in this direction/way)
        and that the corresponding enpassant capture tile for this movement contains
        an enemy pawn piece to capture.*/
        private bool IsEnPassantCaptureLegal(FormedMove move, ChessPosition cpm)
        {
            bool isLegalEnPassantCapture = true;
            Tuple<int, int> posA = move.PosA;
            Tuple<int, int> posB = move.PosB;
            Tile tileA = cpm.Board[posA.Item1, posA.Item2];
            List<List<Tuple<int, int>>> epMovementRays;
            // get attack ray for the pawn on tileA
            // these are the movement rays in the case of EP
            epMovementRays = GetPieceRayPawnCapture(tileA.piece.Val, posA);
            List<Tuple<int, int>> moveRayUsed = null;
            foreach(List<Tuple<int, int>> ray in epMovementRays)
            {
                if (ray.Contains(posB))
                {
                    moveRayUsed = ray;
                    break;
                }
            }
            // so at this point:
            // have posA and posB and it is a valid enpassant-type
            // movement (diagonal to empty square)

            if (moveRayUsed != null)
            {
                // continue checking ep position
                int epX = posB.Item2;
                int epY = posA.Item1;
                Tuple<int, int> posEP = Tuple.Create(epY, epX);
                // if this computed ep square between posA and posB does not 
                // match the currently valid EnPassantSq, then this is not
                // a valid EP capture
                if (!posEP.Equals(cpm.EnPassantSq))
                    isLegalEnPassantCapture = false;

            }
            else
                isLegalEnPassantCapture = false;

            return isLegalEnPassantCapture;
        }


        /* Takes a move which so far meets the requirements for a Capture type
        move on the chess board. This function checks that the one of the capturer
        piece's rays covers this move object (The piece itself can move in this way)
        and that the way is not blocked by another piece en route.
        
        Note: This is very similar to the IsPieceMovementLegal function, only 
        difference is that the check for pieces on a tile is moved after the 
        check for the final tile (since the final tile will have a piece on it:
        the piece being captured, and dont want the code to consider that as an
        intermediate piece which would block the capture. 
        
        Other difference is that if the capturer piece is of type pawn, a unique
        set of pawn capture rays are gotten (Pawn only piece to use different rays
        for capture and movement). */
        private bool IsCaptureLegal(FormedMove move, ChessPosition cpm)
        {
            bool isLegalCapture = true;

            Tuple<int, int> posA = move.PosA;
            Tuple<int, int> posB = move.PosB;

            Tile tileA = cpm.Board[posA.Item1, posA.Item2];
            List<List<Tuple<int, int>>> captureRays;

            if (tileA.piece.Val == EGamePieces.BlackPawn || tileA.piece.Val == EGamePieces.WhitePawn)
                captureRays = GetPieceRayPawnCapture(tileA.piece.Val, posA);
            else
                captureRays = GetPieceRay(tileA.piece.Val, posA);

            List<Tuple<int, int>> captureRayUsed = null;
            foreach (List<Tuple<int, int>> ray in captureRays)
            {
                if (ray.Contains(posB))
                {
                    captureRayUsed = ray;
                    break;
                }
            }
            if (captureRayUsed != null)
                // check there are no intermediate Pieces blocking the capture movement
                foreach (Tuple<int, int> position in captureRayUsed)
                {
                    if (position.Equals(posB))
                        break;
                    Tile tileAtPosition = cpm.Board[position.Item1, position.Item2];
                    if (!tileAtPosition.IsEmpty())
                    {
                        isLegalCapture = false;
                        break;
                    } 
                }
            else
                isLegalCapture = false;

            return isLegalCapture;
        }


        /* This function receives a move object and the chess game and checks if the move also
        includes a pawn promotion, if it does it prompts the player for the selection piece, and
        adds it to the FormedMove. If it does not it ends silently
        TODO: could change it back to return bool, ref the selection piece then add it to move in the calling code ~~style issue*/
        private void MoveIncludesPawnPromotion(ref FormedMove move, ChessPosition cpm)
        {
            
            Piece mvPiece = cpm.Board[move.PosA.Item1, move.PosA.Item2].piece;
            Tuple<int, int> posB = move.PosB;
            // if moving piece is a pawn and posB isHighRank
            if ((mvPiece.Val == EGamePieces.WhitePawn || mvPiece.Val == EGamePieces.BlackPawn) &&
                (IsHighRank(posB)))
            {
                // Safe invoke for popup onto main thread via viewRef
                PromotionSelectionPopup(ref move, mvPiece);
            }
        }


        delegate void PromotionSelectionPopupCallback(ref FormedMove move, Piece mvPiece);
        /* Make a thread safe invoke on the viewRef in order to use it as the parent argument
        to showDialog for the promotion selection form. This is called by Move includes pawn promotion. */
        private void PromotionSelectionPopup(ref FormedMove move, Piece mvPiece)
        {
            if (this.viewRef.InvokeRequired)
            {
                PromotionSelectionPopupCallback d = new PromotionSelectionPopupCallback(PromotionSelectionPopup);
                this.viewRef.Invoke(d, new object[] { move, mvPiece });
            }
            else
            {
                EGamePieces promotionPiece;
                EGamePieces promotionPieceDefault; 
                View.PromotionSelection promotionSelection = new View.PromotionSelection(mvPiece);
                promotionPieceDefault = promotionSelection.DefaultPiece; // (queen)
                // remove the ability to x close the dialog before a piece is selected
                promotionSelection.ControlBox = false;
                if (promotionSelection.ShowDialog(this.viewRef) == System.Windows.Forms.DialogResult.OK)
                {
                    promotionPiece = promotionSelection.SelectedPiece;
                    promotionSelection.Dispose();
                    move.PromotionSelection = promotionPiece;
                }
                else
                    move.PromotionSelection = promotionPieceDefault;
            }
        }


        /* Checks if a position is a high rank on the board
        Used by MoveIncludesPawnPromotion ^ */
        private bool IsHighRank(Tuple<int, int> toSqPos)
        {
            return ((toSqPos.Item1 == 0) || (toSqPos.Item1 == SIZE - 1));
        }


        /* This function is passed a copy of the chess game. It uses the information contained in this object
        to determine whether or not the Current player's king is being threatened by check from the other player
        returning true if so. If it is being checked by the opposing player a list of the one / two coords containing the 
        attacking pieces is stored in 'kingCheckedBy */
        public bool IsKingInCheck(ChessPosition cpmCopy, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            Player curPlayer = cpmCopy.Player;
            Tuple<int, int> curPlayersKingPos = GetCurPlayersKingPos(cpmCopy, curPlayer);

            // using current player's king position, generate all attack vectors
            if (curPlayersKingPos != null)
            {
                // check for threats from the pieces of the opponent
                if (BishopQueenCheck(cpmCopy, curPlayer, curPlayersKingPos, ref kingCheckedBy) ||
                    RookQueenCheck(cpmCopy, curPlayer, curPlayersKingPos, ref kingCheckedBy) ||
                    KnightCheck(cpmCopy, curPlayer, curPlayersKingPos, ref kingCheckedBy) ||
                    KingCheck(cpmCopy, curPlayer, curPlayersKingPos, ref kingCheckedBy) ||
                    PawnCheck(cpmCopy, curPlayer, curPlayersKingPos, ref kingCheckedBy))
                    kingInCheck = true;
            }
            return kingInCheck;
        }


        /* Takes the board and the current player and returns the position of the current
        player's king. */
        private Tuple<int, int> GetCurPlayersKingPos(ChessPosition cpmCopy, Player curPlayer)
        {
            
            Tuple<int, int> curPlayersKingPos = null;
            for (int row = 0; row<cpmCopy.Size; row ++)
            {
                for (int col = 0; col<cpmCopy.Size; col ++)
                {
                    Tile tile = cpmCopy.Board[row, col];
                    if (!tile.IsEmpty())
                    {
                        if ((curPlayer.Owns(tile.piece)) && 
                            (tile.piece.Val == EGamePieces.WhiteKing || tile.piece.Val == EGamePieces.BlackKing))
                        {
                            curPlayersKingPos = Tuple.Create(row, col);
                            break;
                        }
                    }   
                }
            }
            return curPlayersKingPos;
        }


        /* Takes the board, current player, and current players king position and looks for checks on the
        king position from the opponent bishop and diagonal queen. */
        private bool BishopQueenCheck(ChessPosition cpmCopy, Player curPlayer, Tuple<int, int> curPlayersKingPos, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            EGamePieces oppositeBishop = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackBishop : EGamePieces.WhiteBishop;
            EGamePieces oppositeQueen = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackQueen : EGamePieces.WhiteQueen;
            var diagonalRays = GetPieceRay(oppositeBishop, curPlayersKingPos);
            // look through the rays until blocked, if find a bishop or queen not owned by curPlayer
            // then this represents a discovered attack on current Player's king.
            foreach (var ray in diagonalRays)
            {
                foreach (var position in ray)
                {
                    Tile tileAtPosition = cpmCopy.Board[position.Item1, position.Item2];
                    // if position not empty
                    if (!tileAtPosition.IsEmpty())
                    {
                        // if the position is occupied by an attacking bishop/ queen
                        // add this to the kingcheckedbyvalue
                        if (!curPlayer.Owns(tileAtPosition.piece))
                        {
                            if (tileAtPosition.piece.Val == oppositeBishop || tileAtPosition.piece.Val == oppositeQueen)
                            {
                                kingInCheck = true;
                                kingCheckedBy.Add(position);
                            }
                        }
                        // no more threats in this ray, move onto next rays to see if any other threats
                        break;
                    }
                }
            }
            return kingInCheck;
        }


        /* Takes the board, current player, and current players king position and looks for checks on the
        king position from the opponent rook and diagonal queen. */
        private bool RookQueenCheck(ChessPosition cpmCopy, Player curPlayer, Tuple<int, int> curPlayersKingPos, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            EGamePieces oppositeRook = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackRook : EGamePieces.WhiteRook;
            EGamePieces oppositeQueen = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackQueen : EGamePieces.WhiteQueen;
            var adjRays = GetPieceRay(oppositeRook, curPlayersKingPos);
            // look through the rays until blocked, if find a rook or queen not owned by curPlayer
            // then this represents a discovered attack on current Player's king.
            foreach (var ray in adjRays)
            {
                foreach (var position in ray)
                {
                    Tile tileAtPosition = cpmCopy.Board[position.Item1, position.Item2];
                    // if position not empty
                    if (!tileAtPosition.IsEmpty())
                    {
                        // if the position is occupied by an attacking bishop/ queen
                        // add this to the kingcheckedbyvalue
                        if (!curPlayer.Owns(tileAtPosition.piece))
                        {
                            if (tileAtPosition.piece.Val == oppositeRook || tileAtPosition.piece.Val == oppositeQueen)
                            {
                                kingInCheck = true;
                                kingCheckedBy.Add(position);
                            }
                        }
                        // no more threats in this ray, move onto next rays to see if any other threats
                        break;
                    }
                }
            }
            return kingInCheck;
        }


        /* Takes the board, current player, and current players king position and looks for checks on the
        king position from the opponent knight. */
        private bool KnightCheck(ChessPosition cpmCopy, Player curPlayer, Tuple<int, int> curPlayersKingPos, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            EGamePieces oppositeKnight = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackKnight : EGamePieces.WhiteKnight;
            var knRays = GetPieceRay(oppositeKnight, curPlayersKingPos);
            // look through the rays until blocked, if find a knight not owned by curPlayer
            // then this represents a discovered attack on current Player's king.
            foreach (var ray in knRays)
            {
                foreach (var position in ray)
                {
                    Tile tileAtPosition = cpmCopy.Board[position.Item1, position.Item2];
                    // if position not empty
                    if (!tileAtPosition.IsEmpty())
                    {
                        // if the position is occupied by an attacking knight
                        // add this to the kingcheckedbyvalue
                        if (!curPlayer.Owns(tileAtPosition.piece))
                        {
                            if (tileAtPosition.piece.Val == oppositeKnight)
                            {
                                kingInCheck = true;
                                kingCheckedBy.Add(position);
                            }
                        }
                        // no more threats in this ray, move onto next rays to see if any other threats
                        break;
                    }
                }
            }
            return kingInCheck;
        }


        /* Takes the board, current player, and current players king position and looks for checks on the
        king position from the opponent King. */
        private bool KingCheck(ChessPosition cpmCopy, Player curPlayer, Tuple<int, int> curPlayersKingPos, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            EGamePieces oppositeKing = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackKing : EGamePieces.WhiteKing;
            var kRays = GetPieceRay(oppositeKing, curPlayersKingPos);
            // look through the rays until blocked, if find a king not owned by curPlayer
            // then this represents a discovered attack on current Player's king.
            foreach (var ray in kRays)
            {
                foreach (var position in ray)
                {
                    Tile tileAtPosition = cpmCopy.Board[position.Item1, position.Item2];
                    // if position not empty
                    if (!tileAtPosition.IsEmpty())
                    {
                        // if the position is occupied by an attacking king
                        // add this to the kingcheckedbyvalue
                        if (!curPlayer.Owns(tileAtPosition.piece))
                        {
                            if (tileAtPosition.piece.Val == oppositeKing)
                            {
                                kingInCheck = true;
                                kingCheckedBy.Add(position);
                            }
                        }
                        // no more threats in this ray, move onto next rays to see if any other threats
                        break;
                    }
                }
            }
            return kingInCheck;
        }


        /* Takes the board, current player, and current players king position and looks for checks on the
        king position from the opponent Pawn. */
        private bool PawnCheck(ChessPosition cpmCopy, Player curPlayer, Tuple<int, int> curPlayersKingPos, ref List<Tuple<int, int>> kingCheckedBy)
        {
            bool kingInCheck = false;
            // the code must look in the advance direction (own pawn / CURRENT) to see the tiles an OPPONENT pawn could be on
            EGamePieces currentPawn = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.WhitePawn : EGamePieces.BlackPawn;
            EGamePieces opponentPawn = (curPlayer.PlayerValue == EGamePlayers.White) ? EGamePieces.BlackPawn : EGamePieces.WhitePawn;
            var pRays = GetPieceRayPawnCapture(currentPawn, curPlayersKingPos);
            // look through the rays until blocked, if find a Pawn not owned by curPlayer
            // then this represents a discovered attack on current Player's king.
            foreach (var ray in pRays)
            {
                foreach (var position in ray)
                {
                    Tile tileAtPosition = cpmCopy.Board[position.Item1, position.Item2];
                    // if position not empty
                    if (!tileAtPosition.IsEmpty())
                    {
                        // if the position is occupied by an attacking pawn
                        // add this to the kingcheckedbyvalue
                        if (!curPlayer.Owns(tileAtPosition.piece))
                        {
                            if (tileAtPosition.piece.Val == opponentPawn)
                            {
                                kingInCheck = true;
                                kingCheckedBy.Add(position);
                            }
                        }
                        // no more threats in this ray, move onto next rays to see if any other threats
                        break;
                    }
                }
            }
            return kingInCheck;
        }



        /* Takes a piece and a y,x board location and returns one List containing a List for each valid 
        direction of movement. Each of these lists contains a sequence of board locations that the piece 
        could potentially move to (from that starting location) */
        public List<List<Tuple<int, int>>> GetPieceRay(EGamePieces piece, Tuple<int, int> location)
        {
            if (rayArray == null)
                throw new Exception("ray array not instantiated");
            return rayArray[(int)piece][location.Item1, location.Item2];
        }


        /* Same as GetPieceRay but takes a pawn piece and returns the diagonal attack rays rather than the vertical
        pawn movement ray.*/
        public List<List<Tuple<int,int>>> GetPieceRayPawnCapture(EGamePieces piece, Tuple<int, int> location)
        {
            if (rayArrayPawnCapture == null)
                throw new Exception("ray array (pawns) noy instantiated");
            int pieceIndex = (piece == EGamePieces.WhitePawn) ? 0 : 1;
            return rayArrayPawnCapture[pieceIndex][location.Item1, location.Item2];
        }



        /* Generates the rays for use by the GetPieceRay procedure. This method is called at the start of
        the program to eliminate the time cost of computing these rays on a as-i-need-it while the game
        is running.*/
        public void GenerateRays()
        {
            rayArray = new List<List<Tuple<int,int>>>[UNIQUE_PIECE_NUM][,];
            // 12 pieces
            //      8 rows per piece
            //              8 columns per row
            //                      1 List of
            //                              n Lists
            //                                  n Tuple<int, int>

            for (int i = 0; i < rayArray.Length; i++)
            {
                // foreach of the i piece types, create 64 tiles each containing a number of rays (n directions)
                MovementStyle style = Styles.getMovementStyle((EGamePieces)i);
                rayArray[i] = new List<List<Tuple<int,int>>>[SIZE, SIZE];
                for (int j = 0; j < SIZE; j++) // row
                {
                    for (int k = 0; k < SIZE; k++) // col
                    {
                        List<List<Tuple<int, int>>> rays = new List<List<Tuple<int, int>>>();
                        foreach (Tuple<int, int> dir in style.dirs)
                        {
                            List<Tuple<int, int>> ray = new List<Tuple<int, int>>();
                            int coordsNum = 1;
                            while (coordsNum <= style.maxIterations)
                            {
                                //generate the new coordinate by adding 1 unit of direction to the initial posA
                                Tuple<int, int> newPos;
                                int newPosRank = j + (coordsNum * dir.Item1);
                                int newPosFile = k + (coordsNum * dir.Item2);
                                newPos = Tuple.Create(newPosRank, newPosFile);
                                // if the newPos is not on the board, dont add it to the list
                                // and break since movement along this direction cannot continue
                                if ((newPosRank < 0 || newPosRank > 7) ||
                                    (newPosFile < 0 || newPosFile > 7))
                                    break;
                                ray.Add(newPos);
                                coordsNum++;
                            }
                            rays.Add(ray);
                        }
                        rayArray[i][j, k] = rays;
                    }
                }
            }
        }


        /* Generates the rays for use by the GetPieceRayPawnCapture procedure. This method is called at the start of
        the program to eliminate the time cost of computing these rays on a as-i-need-it while the game
        is running.*/
        public void GeneratePawnRays()
        {
            // 2 unique pawn pieces
            rayArrayPawnCapture = new List<List<Tuple<int, int>>>[2][,];
            CaptureStyle WhitePawnCaptureStyle = Styles.getCaptureStyle(EGamePieces.WhitePawn);
            CaptureStyle BlackPawnCaptureStyle = Styles.getCaptureStyle(EGamePieces.BlackPawn);

            rayArrayPawnCapture[0] = new List<List<Tuple<int, int>>>[SIZE, SIZE]; // white
            rayArrayPawnCapture[1] = new List<List<Tuple<int, int>>>[SIZE, SIZE]; // black

            for (int j = 0; j < SIZE; j ++)
            {
                for (int k = 0; k < SIZE; k ++)
                {
                    List<List<Tuple<int, int>>> whiteRays = new List<List<Tuple<int, int>>>();
                    List<List<Tuple<int, int>>> blackRays = new List<List<Tuple<int, int>>>();

                    foreach (Tuple<int, int> dir in WhitePawnCaptureStyle.dirs)
                    {
                        List<Tuple<int, int>> whiteRay = new List<Tuple<int, int>>();
                        Tuple<int, int> newPos;
                        int newPosRank = j + dir.Item1;
                        int newPosFile = k + dir.Item2;
                        newPos = Tuple.Create(newPosRank, newPosFile);
                        if ((newPosRank < 0 || newPosRank > 7) ||
                            (newPosFile < 0 || newPosFile > 7))
                            break;
                        whiteRay.Add(newPos);
                        whiteRays.Add(whiteRay);
                    }

                    foreach (Tuple<int, int> dir in BlackPawnCaptureStyle.dirs)
                    {
                        List<Tuple<int, int>> blackRay = new List<Tuple<int, int>>();
                        Tuple<int, int> newPos;
                        int newPosRank = j + dir.Item1;
                        int newPosFile = k + dir.Item2;
                        newPos = Tuple.Create(newPosRank, newPosFile);
                        if ((newPosRank < 0 || newPosRank > 7) ||
                            (newPosFile < 0 || newPosFile > 7))
                            break;
                        blackRay.Add(newPos);
                        blackRays.Add(blackRay);
                    }

                    rayArrayPawnCapture[0][j, k] = whiteRays;
                    rayArrayPawnCapture[1][j, k] = blackRays;
                }
            }
        }

        public System.Windows.Forms.Form ViewRef
        {
            set
            {
                this.viewRef = value;
            }
        }
    }
}
