using DBChatAI.Module.BusinessObjects.AI;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Blazor;
using DevExpress.ExpressApp.Blazor.Components;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using Microsoft.AspNetCore.Components;

namespace DBChatAI.Blazor.Server.Editors.AiChatViewItem
{

    // XAF model: appears in Application Model → ViewItems
    public interface IModelAiChatDetailViewItemBlazor : IModelViewItem { }

    [ViewItem(typeof(IModelAiChatDetailViewItemBlazor))]
    public class AiChatDetailViewItemBlazor : ViewItem, IComplexViewItem
    {
        // Holder similar to ButtonHolder from the sample
        public class AiChatHolder : IComponentContentHolder
        {
            public AiChatHolder(AiChatModel componentModel)
            {
                ComponentModel = componentModel;
            }

            public AiChatModel ComponentModel { get; }

            RenderFragment IComponentContentHolder.ComponentContent
                => ComponentModelObserver.Create(
                    ComponentModel,
                    AiChatRenderer.Create(ComponentModel)); // 👈 hook to the Razor component
        }

        
        private IObjectSpace chatObjectSpace;


        public AiChatDetailViewItemBlazor(IModelViewItem model, Type objectType)
            : base(objectType, model.Id) { }

        void IComplexViewItem.Setup(IObjectSpace objectSpace, XafApplication application)
        {

            chatObjectSpace = application.CreateObjectSpace(typeof(AiChatSession));
        }

        // DevExpress requires CreateControlCore to return an IComponentContentHolder
        protected override object CreateControlCore()
        {
            var model = new AiChatModel
            {
                // 👇 pass the chatObjectSpace, NOT the viewObjectSpace
                ObjectSpace = chatObjectSpace
            };

            return new AiChatHolder(model);
        }

        protected override void OnControlCreated()
        {
            // here you could set other properties of the model if needed
            base.OnControlCreated();
        }

        public override void BreakLinksToControl(bool unwireEventsOnly)
        {
            // if we had event handlers on ComponentModel, we would unregister them here
            base.BreakLinksToControl(unwireEventsOnly);
        }
    }
}

