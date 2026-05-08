namespace IsoTp.CANBus.Net
{
    public class IsoTpParams
    {
        public uint PhysicalId { get; set; }
        public uint FunctionalId { get; set; }
        public uint ResponseId { get; set; }

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
