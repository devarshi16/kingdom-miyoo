/* Stand-in for libgssapi_krb5.so.2, which libmono-native.so links but OnionOS lacks. Only Kerberos
 * auth in System.Net.Security uses it (never this game). The three data symbols must resolve at load
 * time and every gss_* function must exist with the library's symbol version; they all fail
 * (GSS_S_FAILURE). */
void *GSS_C_NT_HOSTBASED_SERVICE, *GSS_C_NT_USER_NAME, *gss_mech_krb5;
#define FAIL(f) unsigned f(void) { return 0xD0000u; }
FAIL(gss_accept_sec_context)
FAIL(gss_acquire_cred)
FAIL(gss_acquire_cred_with_password)
FAIL(gss_delete_sec_context)
FAIL(gss_display_name)
FAIL(gss_display_status)
FAIL(gss_import_name)
FAIL(gss_indicate_mechs)
FAIL(gss_init_sec_context)
FAIL(gss_inquire_context)
FAIL(gss_oid_equal)
FAIL(gss_release_buffer)
FAIL(gss_release_cred)
FAIL(gss_release_name)
FAIL(gss_release_oid_set)
FAIL(gss_unwrap)
FAIL(gss_wrap)
