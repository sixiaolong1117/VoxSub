// macOS Dock 图标设置 - 原生库
// 编译: cc -dynamiclib -framework AppKit -o libMacDockIcon.dylib MacDockIcon.m
#import <AppKit/AppKit.h>

void SetDockIcon(const char *iconPath)
{
    @autoreleasepool {
        NSString *path = [NSString stringWithUTF8String:iconPath];
        NSImage *image = [[NSImage alloc] initWithContentsOfFile:path];
        if (image) {
            [NSApp setApplicationIconImage:image];
        }
    }
}
