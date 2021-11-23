using Reactive.Bindings.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskTimeAmasser
{
    public class Behavior
    {
    }

    public class CommandConverter : DelegateConverter<EventArgs, (object sender, EventArgs args)>
    {
        protected override (object sender, EventArgs args) OnConvert(EventArgs source) => (AssociateObject, source);
    }
}
