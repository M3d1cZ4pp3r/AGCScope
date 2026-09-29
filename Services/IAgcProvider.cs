using System;
using System.Collections.Generic;
using System.Text;

namespace AGCScope.Services
{   public interface IAgcProvider
    {
        void RefreshMemory();
        ushort Read(uint bank, uint address);
    }
}
