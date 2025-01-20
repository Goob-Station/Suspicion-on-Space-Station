using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Ahelp rate limit values are accounted in periods of this size (seconds).
    ///     After the period has passed, the count resets.
    /// </summary>
    /// <seealso cref="AhelpRateLimitCount"/>
    public static readonly CVarDef<float> AhelpRateLimitPeriod =
        CVarDef.Create("ahelp.rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     How many ahelp messages are allowed in a single rate limit period.
    /// </summary>
    /// <seealso cref="AhelpRateLimitPeriod"/>
    public static readonly CVarDef<int> AhelpRateLimitCount =
        CVarDef.Create("ahelp.rate_limit_count", 10, CVar.SERVERONLY);

    /// <summary>
    ///     Should the administrator's position be displayed in ahelp.
    ///     If it is is false, only the admin's ckey will be displayed in the ahelp.
    /// </summary>
    /// <seealso cref="AdminUseCustomNamesAdminRank"/>
    /// <seealso cref="AhelpAdminPrefixWebhook"/>
    public static readonly CVarDef<bool> AhelpAdminPrefix =
        CVarDef.Create("ahelp.admin_prefix", false, CVar.SERVERONLY);

    /// <summary>
    ///     Should the administrator's position be displayed in the webhook.
    ///     If it is is false, only the admin's ckey will be displayed in webhook.
    /// </summary>
    /// <seealso cref="AdminUseCustomNamesAdminRank"/>
    /// <seealso cref="AhelpAdminPrefix"/>
    public static readonly CVarDef<bool> AhelpAdminPrefixWebhook =
        CVarDef.Create("ahelp.admin_prefix_webhook", false, CVar.SERVERONLY);

    /// <summary>
    ///     If an admin replies to users from discord, should it use their discord role color? (if applicable)
    ///     Overrides DiscordReplyColor and AdminBwoinkColor.
    /// </summary>
    /// <seealso cref="DiscordReplyColor"/>
    /// <seealso cref="AdminBwoinkColor"/>
    public static readonly CVarDef<bool> UseDiscordRoleColor =
        CVarDef.Create("sss.ahelp.use_discord_role_color", true, CVar.SERVERONLY);

    /// <summary>
    ///     If an admin replies to users from discord, should it use their discord role name? (if applicable)
    /// </summary>
    public static readonly CVarDef<bool> UseDiscordRoleName =
        CVarDef.Create("sss.ahelp.use_discord_role_name", true, CVar.SERVERONLY);

    /// <summary>
    ///     The text before an admin's name when replying from discord to indicate they're speaking from discord.
    /// </summary>
    public static readonly CVarDef<string> DiscordReplyPrefix =
        CVarDef.Create("sss.ahelp.discord_reply_prefix", "(DISCORD) ", CVar.SERVERONLY);

    /// <summary>
    ///     The color of the names of admins. This is the fallback color for admins.
    /// </summary>
    /// <seealso cref="UseAdminOOCColorInBwoinks"/>
    /// <seealso cref="UseDiscordRoleColor"/>
    /// <seealso cref="DiscordReplyColor"/>
    public static readonly CVarDef<string> AdminBwoinkColor =
        CVarDef.Create("sss.ahelp.admin_bwoink_color", "red", CVar.SERVERONLY);

    /// <summary>
    ///     The color of the names of admins who reply from discord. Leave empty to disable.
    ///     Unused if UseDiscordRoleColor is true.
    ///     Overrides AdminBwoinkColor.
    /// </summary>
    /// <seealso cref="UseDiscordRoleColor"/>
    /// <seealso cref="AdminBwoinkColor"/>
    public static readonly CVarDef<string> DiscordReplyColor =
        CVarDef.Create("sss.ahelp.discord_reply_color", string.Empty, CVar.SERVERONLY);
    
    /// <summary>
    ///     Use the admin's Admin OOC color in bwoinks.
    ///     If either the ooc color or this is not set, uses the admin.admin_bwoink_color value.
    /// </summary>
    /// <seealso cref="AdminBwoinkColor"/>
    public static readonly CVarDef<bool> UseAdminOOCColorInBwoinks =
        CVarDef.Create("sss.ahelp.bwoink_use_admin_ooc_color", true, CVar.SERVERONLY);
}
