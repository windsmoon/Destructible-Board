namespace Windsmoon.DesctructibleBoard
{
    public readonly struct DelaunayTriangle
    {
        #region fields
        public readonly int A;
        public readonly int B;
        public readonly int C;
        #endregion

        #region constructors
        public DelaunayTriangle(int a, int b, int c)
        {
            A = a;
            B = b;
            C = c;
        }
        #endregion
    }
}