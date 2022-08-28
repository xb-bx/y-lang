include "std/StringBuilder.y";
include "xml.y";
dllimport "libui.dll" 
{
    extern fn uiInit(opts: *uiInitOpts): *char;
    extern fn uiNewWindow(title: *char, width: i32, height: i32, hasMenubar: i32): *uiWindow;
    extern fn uiControlShow(control: *IControl);
    extern fn uiControlDestroy(control: *IControl);
    extern fn uiButtonOnClicked(btn: *uiButton, func: fn (*uiButton, *void): void, data: *void);
    extern fn uiMain();
    extern fn uiButtonSetText(btn: *uiButton, text: *char);
    extern fn uiWindowSetChild(window: *uiWindow, child: *IControl);
    extern fn uiNewButton(str: *char): *uiButton;
    extern fn uiButtonText(btn: *uiButton): *char;
    extern fn uiOnShouldQuit(func: fn (*void): i32, data: *void);
    extern fn uiWindowOnClosing(window: *uiWindow, handler: fn (*uiWindow, *void) : i32, data: *void);
    extern fn uiBoxAppend(ubox: *uiBox, control: *IControl, stretchy: bool);
    extern fn uiBoxDelete(ubox: *uiBox, index: i32);
    extern fn uiBoxPadded(ubox: *uiBox): i32;
    extern fn uiBoxSetPadded(ubox: *uiBox, padded: i32);
    extern fn uiNewHorizontalBox(): *uiBox;
    extern fn uiNewVerticalBox(): *uiBox;
    extern fn uiLabelText(lbl: *uiLabel): *char;
    extern fn uiLabelSetText(lbl: *uiLabel, text: *char);
    extern fn uiNewLabel(text: *char): *uiLabel;
    extern fn uiUninit();
    extern fn uiQuit();
}
struct uiControl {}
interface IControl 
{
    fn nothing();
}
struct uiBox : IControl 
{
    fn nothing() {}
    fn append(control: *IControl, stretchy: bool) 
    {
        uiBoxAppend(this, control, stretchy);
    }
    fn delete(index: i32) 
    {
        uiBoxDelete(this, index);
    }
    fn padded(): i32 
    {
        ret uiBoxPadded(this);
    }
    fn setPadded(padded: i32) 
    {
        uiBoxSetPadded(this, padded);
    }
}
struct uiLabel : IControl 
{
    fn nothing() {}
    fn text(): *char 
    {
        ret uiLabelText(this);
    }
    fn setText(text: *char) 
    {
        uiLabelSetText(this, text);
    }
}

struct uiButton : IControl 
{
    fn nothing() {}
    fn onClick(handler: fn (*uiButton, *void): void, data: *void) 
    {
        uiButtonOnClicked(this, handler, data);
    }
    fn setText(text: *char) 
    {
        uiButtonSetText(this, text);
    }
    fn text(): *char 
    {
        ret uiButtonText(this);
    }
}
struct uiWindow : IControl
{ 
    bullshit: u64;
    fn nothing() {}
    fn uiControl(): *uiControl
    {
        ret cast(this, *uiControl);
    }
    fn setChild(child: *IControl) 
    {
        uiWindowSetChild(this, child);
    }
}
struct uiInitOpts
{
    size: u64;
}