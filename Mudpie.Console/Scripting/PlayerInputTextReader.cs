namespace Mudpie.Console.Scripting
{
    using System.IO;

    internal class PlayerInputTextReader : StreamReader
    {
        public PlayerInputTextReader(Stream stream) : base(stream)
        {
        }

        public override string ReadLine()
        {
            return base.ReadLine();
        }
    }
}
