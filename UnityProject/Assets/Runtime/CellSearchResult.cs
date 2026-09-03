namespace Windsmoon.DesctructibleBoard
{
    /// <summary>
    /// Identifies a cell found by a breadth-first neighbor search and the
    /// graph depth at which it was found.
    /// </summary>
    public readonly struct CellSearchResult
    {
        #region properties
        public int CellId { get; }
        public int Depth { get; }
        #endregion

        #region constructors
        public CellSearchResult(int cellId, int depth)
        {
            CellId = cellId;
            Depth = depth;
        }
        #endregion
    }
}
