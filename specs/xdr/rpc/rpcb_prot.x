/*
 * rpcb_prot.x
 * Vendored from RFC 1833 Section 2.1.
 */

const RPCB_PORT = 111;

struct rpcb {
    unsigned long r_prog;
    unsigned long r_vers;
    string r_netid<>;
    string r_addr<>;
    string r_owner<>;
};

struct rp__list {
    rpcb rpcb_map;
    struct rp__list *rpcb_next;
};

typedef rp__list *rpcblist_ptr;

struct rpcb_rmtcallargs {
    unsigned long prog;
    unsigned long vers;
    unsigned long proc;
    opaque args<>;
};

struct rpcb_rmtcallres {
    string addr<>;
    opaque results<>;
};

struct rpcb_entry {
    string r_maddr<>;
    string r_nc_netid<>;
    unsigned long r_nc_semantics;
    string r_nc_protofmly<>;
    string r_nc_proto<>;
};

struct rpcb_entry_list {
    rpcb_entry rpcb_entry_map;
    struct rpcb_entry_list *rpcb_entry_next;
};

typedef rpcb_entry_list *rpcb_entry_list_ptr;

const rpcb_highproc_2 = RPCBPROC_CALLIT;
const rpcb_highproc_3 = RPCBPROC_TADDR2UADDR;
const rpcb_highproc_4 = RPCBPROC_GETSTAT;

const RPCBSTAT_HIGHPROC = 13;
const RPCBVERS_STAT = 3;
const RPCBVERS_4_STAT = 2;
const RPCBVERS_3_STAT = 1;
const RPCBVERS_2_STAT = 0;

struct rpcbs_addrlist {
    unsigned long prog;
    unsigned long vers;
    int success;
    int failure;
    string netid<>;
    struct rpcbs_addrlist *next;
};

struct rpcbs_rmtcalllist {
    unsigned long prog;
    unsigned long vers;
    unsigned long proc;
    int success;
    int failure;
    int indirect;
    string netid<>;
    struct rpcbs_rmtcalllist *next;
};

typedef int rpcbs_proc[RPCBSTAT_HIGHPROC];
typedef rpcbs_addrlist *rpcbs_addrlist_ptr;
typedef rpcbs_rmtcalllist *rpcbs_rmtcalllist_ptr;

struct rpcb_stat {
    rpcbs_proc info;
    int setinfo;
    int unsetinfo;
    rpcbs_addrlist_ptr addrinfo;
    rpcbs_rmtcalllist_ptr rmtinfo;
};

typedef rpcb_stat rpcb_stat_byvers[RPCBVERS_STAT];

struct netbuf {
    unsigned int maxlen;
    opaque buf<>;
};

program RPCBPROG {
    version RPCBVERS {
        bool RPCBPROC_SET(rpcb) = 1;
        bool RPCBPROC_UNSET(rpcb) = 2;
        string RPCBPROC_GETADDR(rpcb) = 3;
        rpcblist_ptr RPCBPROC_DUMP(void) = 4;
        rpcb_rmtcallres RPCBPROC_CALLIT(rpcb_rmtcallargs) = 5;
        unsigned int RPCBPROC_GETTIME(void) = 6;
        netbuf RPCBPROC_UADDR2TADDR(string) = 7;
        string RPCBPROC_TADDR2UADDR(netbuf) = 8;
    } = 3;

    version RPCBVERS4 {
        bool RPCBPROC_SET(rpcb) = 1;
        bool RPCBPROC_UNSET(rpcb) = 2;
        string RPCBPROC_GETADDR(rpcb) = 3;
        rpcblist_ptr RPCBPROC_DUMP(void) = 4;
        rpcb_rmtcallres RPCBPROC_BCAST(rpcb_rmtcallargs) = RPCBPROC_CALLIT;
        unsigned int RPCBPROC_GETTIME(void) = 6;
        netbuf RPCBPROC_UADDR2TADDR(string) = 7;
        string RPCBPROC_TADDR2UADDR(netbuf) = 8;
        string RPCBPROC_GETVERSADDR(rpcb) = 9;
        rpcb_rmtcallres RPCBPROC_INDIRECT(rpcb_rmtcallargs) = 10;
        rpcb_entry_list_ptr RPCBPROC_GETADDRLIST(rpcb) = 11;
        rpcb_stat_byvers RPCBPROC_GETSTAT(void) = 12;
    } = 4;
} = 100000;