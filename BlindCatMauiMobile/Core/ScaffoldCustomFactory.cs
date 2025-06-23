using BlindCatCore.Core;
using ScaffoldLib.Maui.Args;
using ScaffoldLib.Maui.Containers.Material;
using ScaffoldLib.Maui.Core;

namespace BlindCatMauiMobile.Core;

public class ScaffoldCustomFactory : ViewFactory
{
    public ScaffoldCustomFactory()
    {
        OverrideAgent = CreateAgent;
    }

    private IAgent CreateAgent(CreateAgentArgs args)
    {
        return new AgentMaterial2(args);
    }

    private class AgentMaterial2 : AgentMaterial
    {
        private bool isFirstAppearance = true;
        
        public AgentMaterial2(CreateAgentArgs args) : base(args)
        {
        }

        public override void OnAppear(bool isComplete)
        {
            base.OnAppear(isComplete);

            if (isFirstAppearance)
            {
                isFirstAppearance = false;
                if (ViewWrapper.View.BindingContext is BaseVm vm)
                    vm.OnConnectToNavigation();
            }
        }
    }
}