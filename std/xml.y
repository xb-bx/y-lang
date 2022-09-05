include "std/StringBuilder.y";
struct XmlNode 
{
    name: *char;
    attrs: Attributes;
    children: XmlNodes;
    constructor(name: *char) 
    {
        this.name = name;
        this.children = new XmlNodes();
        this.attrs = new Attributes();
    }
    fn getAttr(name: *char): *Attribute 
    {
        let i: u64 = 0;
        while i < this.attrs.count 
        {
            let attr = this.attrs.data[i];
            if(streq(name, attr.name)) ret attr;
            i = i + 1;
        }
        ret null;
    }
    fn getAttrValueOrDefault(name: *char, default: *char): *char 
    {
        let attr = this.getAttr(name);
        if attr != null ret attr.value;
        ret default;
    }
}
struct Attributes 
{
    data: **Attribute;
    count: u64;
    size: u64;
    constructor() 
    {
        this.size = 8;
        this.count = 0;
        this.data = cast(new_array(8 * 8, typeof(*Attribute)).data, **Attribute);
    }
    fn add(attr: *Attribute) 
    {
        if(this.count == this.size) 
        {
            this.grow();
        }
        this.data[this.count] = attr;
        this.count = this.count + 1;
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newdata = cast(new_array(newsize * 8, typeof(*Attribute)).data, **Attribute);
        let i: u64 = 0;
        while i < this.count 
        {
            newdata[i] = this.data[i];
            i = i + 1;
        }
        this.size = newsize;
        this.data = newdata;
    }
    
}
struct Attribute 
{
    name: *char;
    value: *char;
    constructor(name: *char, value: *char) 
    {
        this.name = name;
        this.value = value;
    }
}
struct XmlNodes 
{
    data: **XmlNode;
    count: u64;
    size: u64;
    constructor() 
    {
        this.size = 8;
        this.count = 0;
        this.data = cast(new_array(this.size * 8, typeof(*XmlNode)).data, **XmlNode);
    }
    fn add(attr: *XmlNode) 
    {
        if(this.count == this.size) 
        {
            this.grow();
        }
        this.data[this.count] = attr;
        this.count = this.count + 1;
    }
    fn grow() 
    {
        let newsize = this.size * 2;
        let newdata = cast(new_array(newsize * 8, typeof(*XmlNode)).data, **XmlNode);
        let i: u64 = 0;
        while i < this.count 
        {
            newdata[i] = this.data[i];
            i = i + 1;
        }
        this.size = newsize;
        this.data = newdata;
    }

}
fn xmlNewNode(name: *char): *XmlNode 
{
    let node = cast(new_obj(56, typeof(XmlNode)).data, *XmlNode);
    let actualnode = new XmlNode(name);
    *node = actualnode;
    ret node;
}
fn xmlNewAttribute(name: *char, value: *char): *Attribute
{
    let node = cast(new_obj(typeof(Attribute).size, typeof(Attribute)).data, *Attribute);
    let actualnode = new Attribute(name, value);
    *node = actualnode;
    ret node;
}
fn charIsLetter(c: char): bool 
{
    let b = cast(c, u8);
    let res = (b >= 65 && b <= 90);
    if (b >= 97 && b <= 122) res = true;
    ret res;
}
fn charIsDigit(c: char): bool 
{
    let b = cast(c, i32);
    ret b >= 48 && b <= 57;
}
fn charIsLetterOrDigit(c: char): bool 
{
    ret charIsLetter(c) || charIsDigit(c);
}
struct XmlParser 
{
    xml: *char;
    len: u64;
    pos: u64;
    error: *char;
    wasErr: bool;
    builder: StringBuilder;
    fn parseXml(xml: *char): *XmlNode 
    {
        ret parseXml(xml, cast(strlen(xml), u64));
    }
    fn parseXml(xml: *char, len: u64): *XmlNode 
    {
        this.xml = xml;
        this.builder = new StringBuilder();
        this.len = len;
        this.pos = 0;
        this.wasErr = false;
        this.error = null;
        ret this.parseNode();
    }   
    fn parseNode(): *XmlNode 
    {
        this.parseChar('<');
        let id = this.parseId();
        let node = xmlNewNode(id);
        this.parseAttributes(node);
        this.parseChar('>');
        let ok = true;
        while ok
        {

        this.skipWhitespace();
        if this.currentEqual('<') 
        {
            let temppos = this.pos;
            this.parseChar('<');
            this.skipWhitespace();
            if this.currentEqual('/') { ok = false; this.pos = temppos; }
            else { this.pos = temppos; node.children.add(this.parseNode()); }
        }
        else ok = false;
        }
        this.parseChar('<');
        this.parseChar('/');
        this.parseId();
        this.parseChar('>');
        ret node;
    }
    fn parseAttributes(node: *XmlNode) 
    {
        this.skipWhitespace();
        let id = this.parseId();
        while id != null 
        {
            this.parseChar('=');
            this.parseChar('"');
            let val = this.parseUntil('"');
            let attr = xmlNewAttribute(id, val);
            node.attrs.add(attr);
            id = this.parseId();
        }
    }
    fn currentDontEqual(c: char): bool 
    {
        if this.pos >= this.len ret true;
        ret this.xml[this.pos] != c;
    }
    fn currentEqual(c: char): bool 
    {
        if this.pos >= this.len ret false;
        ret this.xml[this.pos] == c;
    }
    fn parseUntil(c: char): *char
    {
        while this.currentDontEqual(c)
        {
            this.builder.append(this.xml[this.pos]);
            this.pos = this.pos + 1;
        }
        let str = this.builder.build();
        this.builder.clear();
        this.pos = this.pos + 1;
        ret str;
    }
    fn currentIsLetterOrNum(): bool
    {
        if this.pos >= this.len ret false;
        ret charIsLetterOrDigit(this.xml[this.pos]);
    }
    fn parseId(): *char 
    {
        this.skipWhitespace();
        while this.currentIsLetterOrNum()
        {
            this.builder.append(this.xml[this.pos]);
            this.pos = this.pos + 1;
        }
        if this.builder.count == 0 ret null;
        let str = this.builder.build();
        this.builder.clear();
        ret str;
    }
    fn parseChar(c: char): bool
    {
        this.skipWhitespace();    
        if this.currentDontEqual(c) 
        {
            let builder = new StringBuilder();
            builder.append("expected char ").append(c).append(" but got ").append(this.xml[this.pos]).append(" at ").append(this.pos);
            this.error = builder.build();
            this.wasErr = true; ret false; 
        } else { this.pos = this.pos + 1;  ret true; }
    }
    fn currentIsWhiteSpace(): bool
    {
        if this.pos >= this.len ret false;
        if cast(this.xml[this.pos], u8) <= 32 ret true;
        if this.currentEqual(' ') ret true;
        if this.currentEqual("\r"[0]) ret true;
        if this.currentEqual("\n"[0]) ret true;
        if this.currentEqual("\t"[0]) ret true;
        ret false;
        
    }
    fn skipWhitespace() 
    {
        while this.currentIsWhiteSpace() { this.pos = this.pos + 1; }

    }
}

fn printNode(node: *XmlNode) 
{
    writestr("<");
    writestr(node.name);
    if(node.attrs.count > 0) 
    {
        let i: u64 = 0;
        while i < node.attrs.count
        {
            writestr(" ");
            writestr(node.attrs.data[i].name);
            writestr("=\"");
            writestr(node.attrs.data[i].value);
            writestr("\"");
            i = i + 1;
        }
    }
    writestr(">", 1);
    if(node.children.count > 0) 
    {
        i = 0;
        while i < node.children.count 
        {
            printNode(node.children.data[i]);
            i = i + 1;
        }
    }
    writestr("</");
    writestr(node.name);
    writestr(">");
}
