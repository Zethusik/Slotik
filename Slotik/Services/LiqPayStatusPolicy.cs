namespace Slotik.Services;

public static class LiqPayStatusPolicy
{
    public static bool IsSuccessful(string status) => status is "success" or "wait_compensation" or "sandbox";
    public static bool IsIntermediate(string status) => status is "3ds_verify" or "captcha_verify" or "cvv_verify"
        or "ivr_verify" or "otp_verify" or "password_verify" or "phone_verify" or "pin_verify"
        or "senderapp_verify" or "wait_qr" or "p24_verify" or "mp_verify" or "cash_wait"
        or "hold_wait" or "invoice_wait" or "prepared" or "processing" or "wait_accept"
        or "wait_card" or "wait_lc" or "wait_reserve" or "wait_secure" or "try_again";
}
