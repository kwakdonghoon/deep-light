namespace Abyss
{
    // game.js 의 mulberry32 와 비트 단위로 같은 난수. 같은 시드 = 웹 프로토타입과 같은 월드.
    public sealed class Mulberry32
    {
        uint a;
        public Mulberry32(int seed) { a = (uint)seed; }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (double)(t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
