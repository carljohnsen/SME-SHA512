using System;
using SME;

namespace sme_sha512
{

    [InitializedBus]
    public interface Control : IBus
    {
        bool init { get; set; }
        bool update { get; set; }
        bool finish { get; set; }
    }

    [InitializedBus]
    public interface Status : IBus
    {
        bool busy { get; set; }
    }
    
}