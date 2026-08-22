using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace TAOMBattleBallista.UI
{
    // PrefabExtension on MapBar.xml that injects the SiegeCycle button as a
    // standalone right-anchored ButtonWidget. Same xpath as ROT AI's World
    // Events button, so the two extensions land in the same Children
    // collection — they're independent siblings, ordered visually by
    // MarginRight rather than document order.
    //
    // Margins: ROT AI's button is MarginRight=12, Width=150. Ours is
    // MarginRight=170 (12 + 150 + 8 gap), Width=120, so when both mods are
    // loaded they sit side-by-side without overlap. When ROT AI is absent
    // ours simply sits with extra empty space to its right — harmless.
    //
    // InsertType.Child + Index=int.MaxValue: matches ROT AI's pattern. The
    // xpath targets the <Children> element, so Child appends a child of it.
    [PrefabExtension(
        "MapBar",
        "descendant::Prefab/Window/Widget/Children")]
    public sealed class SiegeCycleMapBarExtension : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Child;
        public override int Index => int.MaxValue;

        [PrefabExtensionText]
        public string ButtonXml => @"
<ButtonWidget DataSource=""{MapNavigation}""
              WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed""
              SuggestedWidth=""120"" SuggestedHeight=""38""
              HorizontalAlignment=""Right"" VerticalAlignment=""Top""
              MarginTop=""8"" MarginRight=""170""
              Brush=""Popup.Done.Button""
              Command.Click=""ExecuteCycleSiege""
              DoNotPassEventsToChildren=""true""
              UpdateChildrenStates=""true"">
  <Children>
    <TextWidget WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent""
                HorizontalAlignment=""Center"" VerticalAlignment=""Center""
                Text=""@SiegeCycleLabel""
                Brush=""Popup.Button.Text""
                Brush.FontSize=""18"" />
  </Children>
</ButtonWidget>";
    }
}
