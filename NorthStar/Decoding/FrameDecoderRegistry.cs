using System;
using System.Collections.Generic;

namespace NorthStar.Decoding
{
    public sealed class FrameDecoderRegistry
    {
        private readonly List<IFrameDecoder> decoders =
            new List<IFrameDecoder>();

        public void Register(
            IFrameDecoder decoder)
        {
            if (decoder == null)
            {
                throw new ArgumentNullException(
                    nameof(decoder));
            }

            decoders.Add(
                decoder);
        }

        public IFrameDecoder GetDecoder(
            Guid sourceFormat)
        {
            foreach (
                IFrameDecoder decoder
                in decoders)
            {
                if (decoder.Supports(
                    sourceFormat))
                {
                    return decoder;
                }
            }

            throw new InvalidOperationException(
                $"No decoder supports source format " +
                $"{sourceFormat}.");
        }
    }
}