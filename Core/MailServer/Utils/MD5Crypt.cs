/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


//
// Oct-07 - Martin Fern�ndez translation for all the linux fans and detractors...
//  
/*
#########################################################
# md5crypt.py
#
# 0423.2000 by michal wallace http://www.sabren.com/
# based on perl's Crypt::PasswdMD5 by Luis Munoz (lem@cantv.net)
# based on /usr/src/libcrypt/crypt.c from FreeBSD 2.2.5-RELEASE
#
# MANY THANKS TO
#
#  Carey Evans - http://home.clear.net.nz/pages/c.evans/
#  Dennis Marti - http://users.starpower.net/marti1/
#
#  For the patches that got this thing working!
#
#########################################################
md5crypt.py - Provides interoperable MD5-based crypt() function

SYNOPSIS

    import md5crypt.py

    cryptedpassword = md5crypt.md5crypt(password, salt);

DESCRIPTION

unix_md5_crypt() provides a crypt()-compatible interface to the
rather new MD5-based crypt() function found in modern operating systems.
It's based on the implementation found on FreeBSD 2.2.[56]-RELEASE and
contains the following license in it:

 "THE BEER-WARE LICENSE" (Revision 42):
 <phk@login.dknet.dk> wrote this file.  As long as you retain this notice you
 can do whatever you want with this stuff. If we meet some day, and you think
 this stuff is worth it, you can buy me a beer in return.   Poul-Henning Kamp

apache_md5_crypt() provides a function compatible with Apache's
.htpasswd files. This was contributed by Bryan Hart <bryan@eai.com>.
*/

//License: http://www.codeproject.com/info/cpol10.aspx
//Link to source: http://www.codeproject.com/Articles/20767/Unix-md5crypt
//C implementation: http://www.opensource.apple.com/source/pam/pam-9/pam/modules/pam_unix/md5_crypt.c

namespace ASC.Mail.Server.Utils;

public class Md5Crypt
{
    /** Password hash magic */
    private static String magic = "$1$";

    /** Characters for base64 encoding */
    private static String itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static byte[] Concat(byte[] left, byte[] right, int max_length = -1)
    {
        var second_length = max_length >= 0 ? max_length : right.Length;
        byte[] concat = new byte[left.Length + second_length];
        Buffer.BlockCopy(left, 0, concat, 0, left.Length);
        Buffer.BlockCopy(right, 0, concat, left.Length, second_length);
        return concat;
    }

    private static String to64(int value, int length)
    {
        StringBuilder result;

        result = new StringBuilder();
        while (--length >= 0)
        {
            result.Append(itoa64.Substring(value & 0x3f, 1));
            value >>= 6;
        }
        return (result.ToString());
    }

    /// <summary>
    /// Unix-like Crypt-MD5 function
    /// </summary>
    /// <param name="password">The user password</param>
    /// <param name="salt">The salt or the pepper of the password</param>
    /// <returns>a human readable string</returns>
    public static String crypt(String password, String salt)
    {
        int saltEnd;
        int len;
        int value;
        int i;
        byte[] final;
        byte[] passwordBytes;
        byte[] saltBytes;
        byte[] ctx;

        StringBuilder result;
        HashAlgorithm x_hash_alg = HashAlgorithm.Create("MD5");

        // Skip magic if it exists
        if (salt.StartsWith(magic))
        {
            salt = salt.Substring(magic.Length);
        }

        // Remove password hash if present
        if ((saltEnd = salt.LastIndexOf('$')) != -1)
        {
            salt = salt.Substring(0, saltEnd);
        }

        // Shorten salt to 8 characters if it is longer
        if (salt.Length > 8)
        {
            salt = salt.Substring(0, 8);
        }

        ctx = Encoding.ASCII.GetBytes(password + magic + salt);
        final = x_hash_alg.ComputeHash(Encoding.ASCII.GetBytes(password + salt + password));

        // Add as many characters of ctx1 to ctx
        for (len = password.Length; len > 0; len -= 16)
        {
            if (len > 16)
            {
                ctx = Concat(ctx, final);
            }
            else
            {
                ctx = Concat(ctx, final, len);

            }
        }

        // Then something really weird...
        passwordBytes = Encoding.ASCII.GetBytes(password);

        for (i = password.Length; i > 0; i >>= 1)
        {
            if ((i & 1) == 1)
            {
                ctx = Concat(ctx, new byte[] { 0 });
            }
            else
            {
                ctx = Concat(ctx, new byte[] { passwordBytes[0] });
            }
        }

        final = x_hash_alg.ComputeHash(ctx);

        // Do additional mutations
        saltBytes = Encoding.ASCII.GetBytes(salt);
        for (i = 0; i < 1000; i++)
        {
            var ctx1 = new byte[] { };
            if ((i & 1) == 1)
            {
                ctx1 = Concat(ctx1, passwordBytes);
            }
            else
            {
                ctx1 = Concat(ctx1, final);
            }
            if (i % 3 != 0)
            {
                ctx1 = Concat(ctx1, saltBytes);
            }
            if (i % 7 != 0)
            {
                ctx1 = Concat(ctx1, passwordBytes);
            }
            if ((i & 1) != 0)
            {
                ctx1 = Concat(ctx1, final);
            }
            else
            {
                ctx1 = Concat(ctx1, passwordBytes);
            }
            final = x_hash_alg.ComputeHash(ctx1);
        }

        result = new StringBuilder();
        // Add the password hash to the result string
        value = ((final[0] & 0xff) << 16) | ((final[6] & 0xff) << 8)
                | (final[12] & 0xff);
        result.Append(to64(value, 4));
        value = ((final[1] & 0xff) << 16) | ((final[7] & 0xff) << 8)
                | (final[13] & 0xff);
        result.Append(to64(value, 4));
        value = ((final[2] & 0xff) << 16) | ((final[8] & 0xff) << 8)
                | (final[14] & 0xff);
        result.Append(to64(value, 4));
        value = ((final[3] & 0xff) << 16) | ((final[9] & 0xff) << 8)
                | (final[15] & 0xff);
        result.Append(to64(value, 4));
        value = ((final[4] & 0xff) << 16) | ((final[10] & 0xff) << 8)
                | (final[5] & 0xff);
        result.Append(to64(value, 4));
        value = final[11] & 0xff;
        result.Append(to64(value, 2));

        // Return result string
        return magic + salt + "$" + result;
    }

}
