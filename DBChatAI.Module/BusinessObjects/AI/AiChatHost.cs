using DevExpress.ExpressApp.DC;
using System.ComponentModel;

namespace DBChatAI.Module.BusinessObjects.AI
{
    [DomainComponent]
    public class AiChatHost
    {

        [Browsable(false)]
        public int ID { get; set; }   // richiesto come chiave logica, anche se non serve
    }
}
