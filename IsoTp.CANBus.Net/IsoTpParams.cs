namespace IsoTp.CANBus.Net
{
    public class IsoTpParams
    {
        public uint PhysicalId { get; set; }
        public uint FunctionalId { get; set; }
        public uint ResponseId { get; set; }
        public byte BLOCK_SIZE { get; set; } = 8;
        public byte SEPARATION_TIME_MIN { get; set; } = 10;     /* 10ms */
        /// <param name="physicalId">Physical ID</param>
        /// <param name="responseId">Respond ID</param>
        /// <param name="functionalId">Functional ID (default 0x7DF)</param>
        public IsoTpParams(uint physicalId, uint responseId, uint functionalId = 0x7DF)
        {
            PhysicalId = physicalId;
            ResponseId = responseId;
            FunctionalId = functionalId;
        }
    }
}
