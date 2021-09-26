﻿namespace DcsBios.Communicator.Configuration
{
    public record OutputString : BiosOutput
    {
        public static string OutputType => "string";

        public int MaxLength { get; set; }
    }
}