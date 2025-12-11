using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Blazor.Components.Models;
namespace DBChatAI.Blazor.Server.Editors.AiChatViewItem;


public class AiChatModel : ComponentModelBase
{
    public AiChatModel() : base() { }

    public IObjectSpace ObjectSpace
    {
        get => GetPropertyValue<IObjectSpace>();
        set => SetPropertyValue(value);
    }
}


