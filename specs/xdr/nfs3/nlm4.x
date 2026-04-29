/*
 * nlm4.x
 * Normalized from RFC 1813 Appendix II and The Open Group Technical Standard
 * "Protocols for Interworking: XNFS, Version 3W", Chapter 14.
 *
 * Those sources describe NLM version 4 as a differential update over the
 * version 3 corpus, so unchanged declarations are carried forward here with
 * versioned names to produce one standalone compilable input.
 */

const LM_MAXSTRLEN = 1024;
const LM_MAXNAMELEN = 1025;
const MAXNETOBJ_SZ = 1024;

typedef opaque netobj<MAXNETOBJ_SZ>;

typedef unsigned hyper uint64;
typedef hyper int64;
typedef unsigned long uint32;
typedef long int32;

enum nlm4_stats {
    NLM4_GRANTED = 0,
    NLM4_DENIED = 1,
    NLM4_DENIED_NOLOCKS = 2,
    NLM4_BLOCKED = 3,
    NLM4_DENIED_GRACE_PERIOD = 4,
    NLM4_DEADLCK = 5,
    NLM4_ROFS = 6,
    NLM4_STALE_FH = 7,
    NLM4_FBIG = 8,
    NLM4_FAILED = 9
};

struct nlm4_stat {
    nlm4_stats stat;
};

struct nlm4_res {
    netobj cookie;
    nlm4_stat stat;
};

struct nlm4_holder {
    bool exclusive;
    int32 svid;
    netobj oh;
    uint64 l_offset;
    uint64 l_len;
};

union nlm4_testrply switch (nlm4_stats stat) {
case NLM4_DENIED:
    nlm4_holder holder;
default:
    void;
};

struct nlm4_testres {
    netobj cookie;
    nlm4_testrply test_stat;
};

struct nlm4_lock {
    string caller_name<LM_MAXSTRLEN>;
    netobj fh;
    netobj oh;
    int32 svid;
    uint64 l_offset;
    uint64 l_len;
};

struct nlm4_lockargs {
    netobj cookie;
    bool block;
    bool exclusive;
    nlm4_lock alock;
    bool reclaim;
    int32 state;
};

struct nlm4_cancargs {
    netobj cookie;
    bool block;
    bool exclusive;
    nlm4_lock alock;
};

struct nlm4_testargs {
    netobj cookie;
    bool exclusive;
    nlm4_lock alock;
};

struct nlm4_unlockargs {
    netobj cookie;
    nlm4_lock alock;
};

enum fsh4_mode {
    fsm_DN = 0,
    fsm_DR = 1,
    fsm_DW = 2,
    fsm_DRW = 3
};

enum fsh4_access {
    fsa_NONE = 0,
    fsa_R = 1,
    fsa_W = 2,
    fsa_RW = 3
};

struct nlm4_share {
    string caller_name<LM_MAXSTRLEN>;
    netobj fh;
    netobj oh;
    fsh4_mode mode;
    fsh4_access access;
};

struct nlm4_shareargs {
    netobj cookie;
    nlm4_share share;
    bool reclaim;
};

struct nlm4_shareres {
    netobj cookie;
    nlm4_stats stat;
    int32 sequence;
};

struct nlm4_notify {
    string name<LM_MAXNAMELEN>;
    int32 state;
};

program NLM_PROG {
    version NLM4_VERS {
        void NLMPROC4_NULL(void) = 0;
        nlm4_testres NLMPROC4_TEST(nlm4_testargs) = 1;
        nlm4_res NLMPROC4_LOCK(nlm4_lockargs) = 2;
        nlm4_res NLMPROC4_CANCEL(nlm4_cancargs) = 3;
        nlm4_res NLMPROC4_UNLOCK(nlm4_unlockargs) = 4;
        nlm4_res NLMPROC4_GRANTED(nlm4_testargs) = 5;
        void NLMPROC4_TEST_MSG(nlm4_testargs) = 6;
        void NLMPROC4_LOCK_MSG(nlm4_lockargs) = 7;
        void NLMPROC4_CANCEL_MSG(nlm4_cancargs) = 8;
        void NLMPROC4_UNLOCK_MSG(nlm4_unlockargs) = 9;
        void NLMPROC4_GRANTED_MSG(nlm4_testargs) = 10;
        void NLMPROC4_TEST_RES(nlm4_testres) = 11;
        void NLMPROC4_LOCK_RES(nlm4_res) = 12;
        void NLMPROC4_CANCEL_RES(nlm4_res) = 13;
        void NLMPROC4_UNLOCK_RES(nlm4_res) = 14;
        void NLMPROC4_GRANTED_RES(nlm4_res) = 15;
        nlm4_shareres NLMPROC4_SHARE(nlm4_shareargs) = 20;
        nlm4_shareres NLMPROC4_UNSHARE(nlm4_shareargs) = 21;
        nlm4_res NLMPROC4_NM_LOCK(nlm4_lockargs) = 22;
        void NLMPROC4_FREE_ALL(nlm4_notify) = 23;
    } = 4;
} = 100021;
