using System;

namespace DcsBios.Communicator.DataParsers
{
    public sealed class StringParser : DataParser<string>
    {
        public bool DataReady { get; private set; }

        public int Length { get; }
        private readonly char[] _buffer;
        private readonly ulong _bufferSizeBits;
        private ulong _bufferFilledBits;

        private bool BufferFilled => _bufferFilledBits == _bufferSizeBits;

        public StringParser(in int address, in int length, in string biosCode) : base(address, biosCode)
        {
            if (length is < 1 or > 64)
                throw new ArgumentOutOfRangeException(nameof(length), length,
                    $"Length of {biosCode} should be between 1 and 64, inclusive.");

            Length = length;
            _buffer = new char[length];
            _bufferSizeBits = length == 64 ? ulong.MaxValue : (1UL << length) - 1;
        }

        private void SetCharacter(int index, char ch)
        {
            DataReady = false;
            _buffer[index] = ch;

            if (!BufferFilled) _bufferFilledBits |= 1UL << index;
        }

        public void Reset()
        {
            Array.Clear(_buffer);
            _bufferFilledBits = 0;
            DataReady = false;
            CurrentValue = string.Empty;
        }

        /// <summary>
        /// Builds upon the existing string data
        /// </summary>
        /// <param name="address">BIOS Address the data came from</param>
        /// <param name="data">Data received at the provided BIOS address</param>
        /// <returns></returns>
        public override void AddData(in int address, in int data)
        {
            var done = false;
            if (Address <= address && address < Address + Length)
            {
                SetCharacter(address - Address, (char)(data & 0xff));
                if (address - Address + 1 == Length) done = true;
                if (Address + Length > address + 1)
                {
                    if (address - Address + 2 == Length) done = true;
                    SetCharacter(address - Address + 1, (char)((data >> 8) & 0xff));
                }
            }

            if (done && BufferFilled)
            {
                DataReady = true;
            }

            CurrentValue = new string(_buffer);
        }
    }
}
