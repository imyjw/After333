#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <cstdlib>
#include <cstring>

namespace {
NSString *const After333CredentialService = @"com.after333.account-credentials";

NSString *After333String(const char *value) {
    if (value == nullptr) {
        return nil;
    }

    return [NSString stringWithUTF8String:value];
}

NSMutableDictionary *After333BaseQuery(NSString *key) {
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: After333CredentialService,
        (__bridge id)kSecAttrAccount: key
    } mutableCopy];
}
}

extern "C" const char *After333SecureCredentialGet(const char *rawKey) {
    @autoreleasepool {
        NSString *key = After333String(rawKey);
        if (key.length == 0) {
            return nullptr;
        }

        NSMutableDictionary *query = After333BaseQuery(key);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;

        CFTypeRef result = nullptr;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (status != errSecSuccess || result == nullptr) {
            if (result != nullptr) {
                CFRelease(result);
            }
            return nullptr;
        }

        NSData *data = CFBridgingRelease(result);
        NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return value == nil ? nullptr : strdup(value.UTF8String);
    }
}

extern "C" int After333SecureCredentialSet(const char *rawKey, const char *rawValue) {
    @autoreleasepool {
        NSString *key = After333String(rawKey);
        NSString *value = After333String(rawValue);
        if (key.length == 0 || value == nil) {
            return 0;
        }

        NSData *data = [value dataUsingEncoding:NSUTF8StringEncoding];
        NSMutableDictionary *query = After333BaseQuery(key);
        NSMutableDictionary *existenceQuery = [query mutableCopy];
        existenceQuery[(__bridge id)kSecReturnAttributes] = @YES;
        existenceQuery[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef existingResult = nullptr;
        OSStatus existingStatus = SecItemCopyMatching(
            (__bridge CFDictionaryRef)existenceQuery,
            &existingResult);
        if (existingResult != nullptr) {
            CFRelease(existingResult);
        }

        if (existingStatus == errSecSuccess) {
            NSDictionary *attributes = @{
                (__bridge id)kSecValueData: data,
                (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            };
            return SecItemUpdate(
                (__bridge CFDictionaryRef)query,
                (__bridge CFDictionaryRef)attributes) == errSecSuccess ? 1 : 0;
        }

        if (existingStatus != errSecItemNotFound) {
            return 0;
        }

        query[(__bridge id)kSecValueData] = data;
        query[(__bridge id)kSecAttrAccessible] =
            (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
        return SecItemAdd((__bridge CFDictionaryRef)query, nullptr) == errSecSuccess ? 1 : 0;
    }
}

extern "C" int After333SecureCredentialDelete(const char *rawKey) {
    @autoreleasepool {
        NSString *key = After333String(rawKey);
        if (key.length == 0) {
            return 0;
        }

        OSStatus status = SecItemDelete((__bridge CFDictionaryRef)After333BaseQuery(key));
        return status == errSecSuccess || status == errSecItemNotFound ? 1 : 0;
    }
}

extern "C" void After333SecureCredentialFree(const char *pointer) {
    if (pointer != nullptr) {
        free(const_cast<char *>(pointer));
    }
}
