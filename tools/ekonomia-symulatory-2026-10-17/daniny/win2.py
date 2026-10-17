import sys
sys.path.insert(0, sys.argv[1])
import parse2
days = parse2.run(sys.argv[2])
ds = sorted(d for d in days if d > 0)
keys = ['K_wsie_zakupy','H_pobrano','H_korony','T_wsie','T_jalmuzna_wsi','Z_wsie_kupione','K_W1_zl','BK_rynek_wsi','G_korona','C_danina_osad','M_do_kies','B_zwolnieni_wsie','L_wsi_pozyczone','U_oddane','G_wies','KH_fracht_wsie','KH_kasa_celu_mala','KH_zamowione']
for i in range(0, len(ds), 20):
    w = ds[i:i+20]
    out = []
    for k in keys:
        v = [days[d][k] for d in w if k in days[d]]
        out.append('%s %.0f' % (k, sum(v)/len(v) if v else float('nan')))
    print('%d-%d: ' % (w[0], w[-1]) + ', '.join(out))
